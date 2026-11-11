using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ContextMenuManager.Methods
{
    /// <summary>
    /// Persistent, per-feature snapshots for registry trees changed by context-menu presets.
    /// Backups are written before any destructive operation and retained until a successful restore.
    /// </summary>
    internal sealed class RegistryBackupTarget
    {
        public string Root { get; private set; }
        public string Path { get; private set; }

        public RegistryBackupTarget(string root, string path)
        {
            Root = root;
            Path = path;
        }
    }

    internal static class RegistryTreeBackupManager
    {
        private const int FormatVersion = 1;
        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("CMMREG01");

        private sealed class KeySnapshot
        {
            public bool Exists;
            public readonly List<ValueSnapshot> Values = new List<ValueSnapshot>();
            public readonly List<ChildSnapshot> Children = new List<ChildSnapshot>();
        }

        private sealed class ValueSnapshot
        {
            public string Name;
            public RegistryValueKind Kind;
            public object Value;
        }

        private sealed class ChildSnapshot
        {
            public string Name;
            public KeySnapshot Key;
        }

        private static string BackupDirectory
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ContextMenuManager", "RegistryBackups");
            }
        }

        public static bool HasBackup(string id)
        {
            return File.Exists(GetBackupPath(id));
        }

        /// <summary>Capture each target once. Existing snapshots are intentionally not overwritten.</summary>
        public static void Capture(string id, params RegistryBackupTarget[] targets)
        {
            if (targets == null || targets.Length == 0) return;
            string path = GetBackupPath(id);
            if (File.Exists(path)) return;

            Directory.CreateDirectory(BackupDirectory);
            string tempPath = path + ".tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    writer.Write(Signature.Length);
                    writer.Write(Signature);
                    writer.Write(FormatVersion);
                    writer.Write(targets.Length);
                    foreach (RegistryBackupTarget target in targets)
                    {
                        writer.Write(target.Root ?? string.Empty);
                        writer.Write(target.Path ?? string.Empty);
                        using (RegistryKey root = OpenRoot(target.Root))
                        using (RegistryKey key = root == null ? null : root.OpenSubKey(target.Path, false))
                        {
                            writer.Write(key != null);
                            if (key != null) WriteKey(writer, key);
                        }
                    }
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tempPath, path);
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }

        public static void DeleteWithBackup(string id, params RegistryBackupTarget[] targets)
        {
            Capture(id, targets);
            foreach (RegistryBackupTarget target in targets)
            {
                using (RegistryKey root = OpenRoot(target.Root))
                {
                    if (root == null) continue;
                    try { root.DeleteSubKeyTree(target.Path, false); }
                    catch (ArgumentException) { }
                }
            }
        }

        /// <summary>Restore captured keys by merging their original values/subkeys into the current registry.</summary>
        public static void Restore(string id)
        {
            string path = GetBackupPath(id);
            if (!File.Exists(path)) return;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                int signatureLength = reader.ReadInt32();
                if (signatureLength != Signature.Length) throw new InvalidDataException("Invalid registry backup signature.");
                byte[] signature = reader.ReadBytes(signatureLength);
                for (int i = 0; i < Signature.Length; i++)
                    if (signature[i] != Signature[i]) throw new InvalidDataException("Invalid registry backup signature.");
                if (reader.ReadInt32() != FormatVersion) throw new InvalidDataException("Unsupported registry backup version.");
                int count = ReadCount(reader, 1000);
                for (int i = 0; i < count; i++)
                {
                    string rootName = reader.ReadString();
                    string subPath = reader.ReadString();
                    bool exists = reader.ReadBoolean();
                    if (!exists) continue;

                    KeySnapshot snapshot = ReadKey(reader);
                    using (RegistryKey root = OpenRoot(rootName))
                    {
                        if (root != null) RestoreKey(root, subPath, snapshot);
                    }
                }
            }

            // Keep the snapshot if restore failed; delete only after all keys have been written.
            File.Delete(path);
        }

        public static void DeleteValue(RegistryKey root, string subPath, string valueName)
        {
            if (root == null) return;
            using (RegistryKey key = root.OpenSubKey(subPath, true))
            {
                if (key != null) key.DeleteValue(valueName, false);
            }
        }

        private static string GetBackupPath(string id)
        {
            string safe = string.IsNullOrWhiteSpace(id) ? "RegistryBackup" : id;
            var chars = safe.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return System.IO.Path.Combine(BackupDirectory, new string(chars) + ".bin");
        }

        private static RegistryKey OpenRoot(string root)
        {
            if (string.Equals(root, "HKCR", StringComparison.OrdinalIgnoreCase)) return Registry.ClassesRoot;
            if (string.Equals(root, "HKLM", StringComparison.OrdinalIgnoreCase)) return Registry.LocalMachine;
            if (string.Equals(root, "HKCU", StringComparison.OrdinalIgnoreCase)) return Registry.CurrentUser;
            throw new ArgumentException("Unsupported registry root: " + root);
        }

        private static void WriteKey(BinaryWriter writer, RegistryKey key)
        {
            var values = new List<ValueSnapshot>();
            foreach (string valueName in key.GetValueNames())
            {
                // Fail the capture if any value cannot be read. DeleteWithBackup will then abort,
                // rather than deleting a key with an incomplete rollback snapshot.
                values.Add(new ValueSnapshot
                {
                    Name = valueName ?? string.Empty,
                    Kind = key.GetValueKind(valueName),
                    Value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                });
            }

            writer.Write(values.Count);
            foreach (ValueSnapshot value in values)
            {
                writer.Write(value.Name);
                writer.Write((int)value.Kind);
                WriteValue(writer, value.Kind, value.Value);
            }

            string[] subKeys = key.GetSubKeyNames();
            writer.Write(subKeys.Length);
            foreach (string name in subKeys)
            {
                writer.Write(name);
                using (RegistryKey child = key.OpenSubKey(name, false))
                {
                    if (child == null)
                        throw new IOException("Could not read registry subkey while creating backup: " + name);
                    writer.Write(true);
                    WriteKey(writer, child);
                }
            }
        }

        private static void WriteValue(BinaryWriter writer, RegistryValueKind kind, object value)
        {
            byte storageType;
            if (value is byte[])
            {
                storageType = 5;
                writer.Write(storageType);
                byte[] bytes = (byte[])value;
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
            else if (value is string[])
            {
                storageType = 4;
                writer.Write(storageType);
                string[] strings = (string[])value;
                writer.Write(strings.Length);
                foreach (string s in strings) writer.Write(s ?? string.Empty);
            }
            else if (kind == RegistryValueKind.DWord)
            {
                storageType = 2;
                writer.Write(storageType);
                writer.Write(Convert.ToInt32(value));
            }
            else if (kind == RegistryValueKind.QWord)
            {
                storageType = 3;
                writer.Write(storageType);
                writer.Write(Convert.ToInt64(value));
            }
            else
            {
                storageType = 1;
                writer.Write(storageType);
                writer.Write(Convert.ToString(value) ?? string.Empty);
            }
        }

        private static KeySnapshot ReadKey(BinaryReader reader)
        {
            var snapshot = new KeySnapshot { Exists = true };
            int valueCount = ReadCount(reader, 100000);
            for (int i = 0; i < valueCount; i++)
            {
                string name = reader.ReadString();
                var kind = (RegistryValueKind)reader.ReadInt32();
                snapshot.Values.Add(new ValueSnapshot { Name = name, Kind = kind, Value = ReadValue(reader) });
            }

            int childCount = ReadCount(reader, 100000);
            for (int i = 0; i < childCount; i++)
            {
                string name = reader.ReadString();
                bool exists = reader.ReadBoolean();
                snapshot.Children.Add(new ChildSnapshot { Name = name, Key = exists ? ReadKey(reader) : null });
            }
            return snapshot;
        }

        private static object ReadValue(BinaryReader reader)
        {
            byte storageType = reader.ReadByte();
            switch (storageType)
            {
                case 2: return reader.ReadInt32();
                case 3: return reader.ReadInt64();
                case 4:
                    int stringCount = ReadCount(reader, 100000);
                    var strings = new string[stringCount];
                    for (int i = 0; i < stringCount; i++) strings[i] = reader.ReadString();
                    return strings;
                case 5:
                    int byteCount = ReadCount(reader, 100000000);
                    byte[] bytes = reader.ReadBytes(byteCount);
                    if (bytes.Length != byteCount) throw new EndOfStreamException();
                    return bytes;
                default: return reader.ReadString();
            }
        }

        private static int ReadCount(BinaryReader reader, int max)
        {
            int value = reader.ReadInt32();
            if (value < 0 || value > max) throw new InvalidDataException("Invalid registry backup length.");
            return value;
        }

        private static void RestoreKey(RegistryKey root, string path, KeySnapshot snapshot)
        {
            using (RegistryKey key = root.CreateSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null) throw new IOException("Could not restore registry key: " + path);
                foreach (ValueSnapshot value in snapshot.Values)
                {
                    RegistryValueKind kind = value.Kind == RegistryValueKind.Unknown ? RegistryValueKind.String : value.Kind;
                    key.SetValue(value.Name, value.Value, kind);
                }

                foreach (ChildSnapshot child in snapshot.Children)
                {
                    if (child.Key != null) RestoreKey(key, child.Name, child.Key);
                }
            }
        }
    }
}

namespace ContextMenuManager.Methods
{
    internal sealed class RegistryBackupValueTarget
    {
        public string Root { get; private set; }
        public string Path { get; private set; }
        public string Name { get; private set; }

        public RegistryBackupValueTarget(string root, string path, string name)
        {
            Root = root;
            Path = path;
            Name = name;
        }
    }

    /// <summary>Persistent snapshots for individual registry values (used for policy and block-list values).</summary>
    internal static class RegistryValueBackupManager
    {
        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("CMMVAL01");
        private const int FormatVersion = 2;

        private sealed class ValueRecord
        {
            public RegistryBackupValueTarget Target;
            public bool KeyExisted;
            public bool Existed;
            public RegistryValueKind Kind;
            public object Value;
        }

        private static string GetPath(string id)
        {
            string safe = string.IsNullOrWhiteSpace(id) ? "RegistryValueBackup" : id;
            var chars = safe.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ContextMenuManager", "RegistryBackups", new string(chars) + ".values.bin");
        }

        public static bool HasBackup(string id) => File.Exists(GetPath(id));

        public static void SetValueWithBackup(string id, RegistryBackupValueTarget target, object value, RegistryValueKind kind)
        {
            Capture(id, target);
            using (RegistryKey root = OpenRoot(target.Root))
            using (RegistryKey key = root?.CreateSubKey(target.Path, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null) throw new IOException("Could not create registry key: " + target.Path);
                key.SetValue(target.Name, value, kind);
            }
        }

        public static void DeleteWithBackup(string id, params RegistryBackupValueTarget[] targets)
        {
            Capture(id, targets);
            foreach (RegistryBackupValueTarget target in targets)
            {
                using (RegistryKey root = OpenRoot(target.Root))
                using (RegistryKey key = root?.OpenSubKey(target.Path, true))
                {
                    if (key != null) key.DeleteValue(target.Name, false);
                }
            }
        }

        public static void Restore(string id)
        {
            string path = GetPath(id);
            if (!File.Exists(path)) return;
            var records = new List<ValueRecord>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                int sigLength = reader.ReadInt32();
                if (sigLength != Signature.Length) throw new InvalidDataException("Invalid registry value backup signature.");
                byte[] sig = reader.ReadBytes(sigLength);
                for (int i = 0; i < Signature.Length; i++)
                    if (sig[i] != Signature[i]) throw new InvalidDataException("Invalid registry value backup signature.");
                if (reader.ReadInt32() != FormatVersion) throw new InvalidDataException("Unsupported registry value backup version.");
                int count = reader.ReadInt32();
                if (count < 0 || count > 10000) throw new InvalidDataException("Invalid registry value backup length.");
                for (int i = 0; i < count; i++)
                {
                    var target = new RegistryBackupValueTarget(reader.ReadString(), reader.ReadString(), reader.ReadString());
                    bool keyExisted = reader.ReadBoolean();
                    bool existed = reader.ReadBoolean();
                    if (!existed)
                    {
                        records.Add(new ValueRecord { Target = target, KeyExisted = keyExisted, Existed = false });
                    }
                    else
                    {
                        var kind = (RegistryValueKind)reader.ReadInt32();
                        records.Add(new ValueRecord { Target = target, KeyExisted = keyExisted, Existed = true, Kind = kind, Value = ReadValue(reader) });
                    }
                }
            }

            foreach (ValueRecord record in records)
            {
                using (RegistryKey root = OpenRoot(record.Target.Root))
                {
                    if (root == null) continue;
                    using (RegistryKey key = record.Existed
                        ? root.CreateSubKey(record.Target.Path, RegistryKeyPermissionCheck.ReadWriteSubTree)
                        : root.OpenSubKey(record.Target.Path, true))
                    {
                        if (key != null)
                        {
                            if (record.Existed)
                            {
                                RegistryValueKind kind = record.Kind == RegistryValueKind.Unknown ? RegistryValueKind.String : record.Kind;
                                key.SetValue(record.Target.Name, record.Value, kind);
                            }
                            else
                            {
                                // This value was absent before the feature was applied; remove only that value.
                                key.DeleteValue(record.Target.Name, false);
                            }
                        }
                    }

                    // Do not leave an empty key that was created solely for the temporary value.
                    if (!record.KeyExisted && !record.Existed)
                    {
                        string keyPath = record.Target.Path ?? string.Empty;
                        int separator = keyPath.LastIndexOf('\\');
                        string parentPath = separator < 0 ? string.Empty : keyPath.Substring(0, separator);
                        string leafName = separator < 0 ? keyPath : keyPath.Substring(separator + 1);
                        using (RegistryKey parent = string.IsNullOrEmpty(parentPath) ? root : root.OpenSubKey(parentPath, true))
                        {
                            if (parent != null)
                            {
                                using (RegistryKey child = parent.OpenSubKey(leafName, false))
                                {
                                    if (child != null && child.ValueCount == 0 && child.SubKeyCount == 0)
                                        parent.DeleteSubKey(leafName, false);
                                }
                            }
                        }
                    }
                }
            }
            File.Delete(path);
        }

        private static void Capture(string id, params RegistryBackupValueTarget[] targets)
        {
            if (targets == null || targets.Length == 0) return;
            string path = GetPath(id);
            if (File.Exists(path)) return;
            string directory = System.IO.Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string tempPath = path + ".tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    writer.Write(Signature.Length);
                    writer.Write(Signature);
                    writer.Write(FormatVersion);
                    writer.Write(targets.Length);
                    foreach (RegistryBackupValueTarget target in targets)
                    {
                        writer.Write(target.Root ?? string.Empty);
                        writer.Write(target.Path ?? string.Empty);
                        writer.Write(target.Name ?? string.Empty);
                        using (RegistryKey root = OpenRoot(target.Root))
                        using (RegistryKey key = root?.OpenSubKey(target.Path, false))
                        {
                            writer.Write(key != null);
                            string[] names = key == null ? new string[0] : key.GetValueNames();
                            bool exists = Array.IndexOf(names, target.Name) >= 0;
                            writer.Write(exists);
                            if (exists)
                            {
                                RegistryValueKind kind = key.GetValueKind(target.Name);
                                object value = key.GetValue(target.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                                writer.Write((int)kind);
                                WriteValue(writer, value);
                            }
                        }
                    }
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tempPath, path);
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }

        private static RegistryKey OpenRoot(string root)
        {
            if (string.Equals(root, "HKCR", StringComparison.OrdinalIgnoreCase)) return Registry.ClassesRoot;
            if (string.Equals(root, "HKLM", StringComparison.OrdinalIgnoreCase)) return Registry.LocalMachine;
            if (string.Equals(root, "HKCU", StringComparison.OrdinalIgnoreCase)) return Registry.CurrentUser;
            throw new ArgumentException("Unsupported registry root: " + root);
        }

        private static void WriteValue(BinaryWriter writer, object value)
        {
            if (value is byte[])
            {
                writer.Write((byte)5);
                byte[] bytes = (byte[])value;
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
            else if (value is string[])
            {
                writer.Write((byte)4);
                string[] values = (string[])value;
                writer.Write(values.Length);
                foreach (string s in values) writer.Write(s ?? string.Empty);
            }
            else if (value is long)
            {
                writer.Write((byte)3);
                writer.Write((long)value);
            }
            else if (value is int)
            {
                writer.Write((byte)2);
                writer.Write((int)value);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(Convert.ToString(value) ?? string.Empty);
            }
        }

        private static object ReadValue(BinaryReader reader)
        {
            switch (reader.ReadByte())
            {
                case 2: return reader.ReadInt32();
                case 3: return reader.ReadInt64();
                case 4:
                    int count = reader.ReadInt32();
                    if (count < 0 || count > 100000) throw new InvalidDataException("Invalid value array length.");
                    var values = new string[count];
                    for (int i = 0; i < count; i++) values[i] = reader.ReadString();
                    return values;
                case 5:
                    int length = reader.ReadInt32();
                    if (length < 0 || length > 100000000) throw new InvalidDataException("Invalid binary value length.");
                    byte[] bytes = reader.ReadBytes(length);
                    if (bytes.Length != length) throw new EndOfStreamException();
                    return bytes;
                default: return reader.ReadString();
            }
        }
    }
}
