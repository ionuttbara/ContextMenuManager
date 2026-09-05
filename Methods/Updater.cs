using BluePointLilac.Controls;
using BluePointLilac.Methods;
using System;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace ContextMenuManager.Methods
{
    sealed class Updater
    {
        /// <summary>定期检查更新</summary>
        public static void PeriodicUpdate()
        {
        }

        /// <summary>更新程序以及程序字典</summary>
        /// <param name="isManual">是否为手动点击更新</param>
        public static void Update(bool isManual)
        {
            AppConfig.LastCheckUpdateTime = DateTime.Today;
            UpdateText(isManual);
        }

        /// <summary>更新程序</summary>
        /// <param name="isManual">是否为手动点击更新</param>
        

        /// <summary>更新程序字典</summary>
        /// <param name="isManual">是否为手动点击更新</param>
        private static void UpdateText(bool isManual)
        {
            
            string[] filePaths;
            

            filePaths = new[]
            {
                AppConfig.WebGuidInfosDic, AppConfig.WebEnhanceMenusDic,
                AppConfig.WebDetailedEditDic, AppConfig.WebUwpModeItemsDic
            };

            filePaths = Directory.GetFiles(AppConfig.LangsDir, "*.ini");

  
        }

        /// <summary>加工处理更新信息，去掉标题头</summary>
        private static string MachinedInfo(string info)
        {
            string str = string.Empty;
            string[] lines = info.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for(int m = 0; m < lines.Length; m++)
            {
                string line = lines[m];
                for(int n = 1; n <= 6; n++)
                {
                    if(line.StartsWith(new string('#', n) + ' '))
                    {
                        line = line.Substring(n + 1);
                        break;
                    }
                }
                str += line + "\r\n";
            }
            return str;
        }
    }
}