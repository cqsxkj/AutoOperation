using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AutoOperationService.FileUnit
{
    class FileTool
    {
        /// <summary>
        /// 复制文件
        /// </summary>
        /// <param name="sourcePath">源目录</param>
        /// <param name="desPath">目标目录</param>
        public void CopyFile(string sourcePath, string desPath)
        {
            if (!Directory.Exists(desPath))
            {
                Directory.CreateDirectory(desPath);
            }
            string[] files = Directory.GetFiles(sourcePath);
            for (int i = 0; i < files.Length; i++)
            {
                string[] childfile = files[i].Split('\\');
                File.Copy(files[i], desPath + @"\" + childfile[childfile.Length - 1], true);
            }
            string[] dirs = Directory.GetDirectories(sourcePath);
            for (int i = 0; i < dirs.Length; i++)
            {
                string[] childdir = dirs[i].Split('\\');
                CopyFile(dirs[i], desPath + @"\" + childdir[childdir.Length - 1]);
            }
        }

        /// <summary>
        /// 创建目录
        /// </summary>
        /// <param name="path">需要创建的目录地址</param>
        public void CreateDirtory(string path)
        {
            if (!File.Exists(path))
            {
                string[] dirArray = path.Split('\\');
                string temp = string.Empty;
                for (int i = 0; i < dirArray.Length - 1; i++)
                {
                    temp += dirArray[i].Trim() + "\\";
                    if (!Directory.Exists(temp))
                        Directory.CreateDirectory(temp);
                }
            }
        }

        /// <summary>
        /// 删除目录及子目录
        /// </summary>
        /// <param name="path">地址</param>
        public void DeleteFile(string path)
        {
            System.IO.Directory.Delete(path, true);
        }
    }
}
