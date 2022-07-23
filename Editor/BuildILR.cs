//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
#pragma warning disable 0618

using EP.U3D.EDITOR.BASE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace EP.U3D.EDITOR.ILR
{
    public class BuildILR
    {
        public static List<string> IgnoreList = new List<string>()
        {
        };

        public static void Execute()
        {
            if (!PrepareDirectory()) return;
            ProcessBuild();
            GenerateManifest();
            string toast = "Compile ILR done.";
            Helper.Log(toast);
            Helper.ShowToast(toast);
        }

        private static bool PrepareDirectory()
        {
            string targetPath = Constants.BUILD_ILR_BUNDLE_PATH;
            if (Directory.Exists(targetPath) == false)
            {
                Directory.CreateDirectory(targetPath);
            }
            else
            {
                Directory.Delete(targetPath, true);
                Directory.CreateDirectory(targetPath);
            }
            return true;
        }

        private static void ProcessBuild()
        {
            string targetPath = Constants.BUILD_ILR_BUNDLE_PATH;

            #region dll references

            List<string> dlls = new List<string>();
            List<string> srcs = new List<string>();
            //所有宏
            defineList = new List<string>();

            string[] parseCsprojList = new string[] { "Assembly-CSharp.csproj" };
            foreach (var csproj in parseCsprojList)
            {
                var path = Path.Combine(Constants.PROJ_PATH, csproj);
                if (!File.Exists(path))
                {
                    EditorUtility.DisplayDialog("Warning", "Assembly-CSharp.csproj doesn't exist", "OK");
                    return;
                }

                ParseCSProj(path, new List<string>() { }, ref srcs, ref dlls);
            }

            //去重
            dlls = dlls.Distinct().ToList();
            srcs = srcs.Distinct().ToList();
            defineList = defineList.Distinct().ToList();

            //移除参与分析csproj的dll,因为已经解析 包含在cs
            foreach (var csproj in parseCsprojList)
            {
                var dll = csproj.Replace(".csproj", ".dll");

                var idx = dlls.FindIndex((d) => d.EndsWith(dll, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    dlls.RemoveAt(idx);
                    //Debug.Log("[Build DLL]剔除:" + dll);
                }
            }

            //宏解析
            //移除editor相关宏
            for (int i = defineList.Count - 1; i >= 0; i--)
            {
                var symbol = defineList[i];
                if (symbol.Contains("UNITY_EDITOR"))
                {
                    defineList.RemoveAt(i);
                }
            }

            //剔除不存的dll
            //TODO 这里是File 接口mac下有bug 会判断文件不存在
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                for (int i = dlls.Count - 1; i >= 0; i--)
                {
                    var dll = dlls[i];
                    if (!File.Exists(dll))
                    {
                        dlls.RemoveAt(i);
                    }
                }
            }
            #endregion

            var csdll = Constants.PROJ_PATH + "/Library/ScriptAssemblies/Assembly-CSharp.dll";
            if (!dlls.Contains(csdll)) dlls.Add(csdll);

            try
            {
                var prefix = Constants.ILR_SCRIPT_WORKSPACE.Replace(Application.dataPath, "");
                var ilrs = srcs.FindAll(f => f.Contains(prefix) && f.EndsWith(".cs"));
                var mainDll = Path.Combine(targetPath, "main" + Constants.ILR_BUNDLE_FILE_EXTENSION);
                BuildByRoslyn(dlls.ToArray(), ilrs.ToArray(), mainDll, false, false);
                Helper.SaveFile(mainDll, LIBRARY.BASE.Helper.EncryptBytes(Helper.OpenFile(mainDll)));
            }
            catch (Exception e)
            {
                Helper.LogError(e.Message);
                EditorUtility.ClearProgressBar();
                return;
            }
            AssetDatabase.Refresh();
        }

        public static List<string> CollectFiles(string directory, List<string> output, string extension)
        {
            if (output == null)
            {
                output = new List<string>();
            }
            if (Directory.Exists(directory))
            {
                string[] files = Directory.GetFiles(directory, extension);
                for (int i = 0; i < files.Length; i++)
                {
                    string file = NormallizePath(files[i]);
                    output.Add(file);
                }
                string[] dirs = Directory.GetDirectories(directory);
                for (int i = 0; i < dirs.Length; i++)
                {
                    CollectFiles(dirs[i], output, extension);
                }
            }
            return output;
        }

        public static string NormallizePath(string path)
        {
            return path.Replace("\\", "/");
        }

        private static void GenerateManifest()
        {
            string targetPath = Constants.BUILD_ILR_BUNDLE_PATH;
            string filePath = targetPath + "/manifest.txt";
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            List<string> files = new List<string>();
            CollectFiles(targetPath, files, "*" + Constants.ILR_BUNDLE_FILE_EXTENSION);
            FileStream fs = new FileStream(filePath, FileMode.CreateNew);
            StreamWriter sw = new StreamWriter(fs);
            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                if (file.EndsWith(".meta")) continue;
                string md5 = Helper.FileMD5(file);
                string value = file.Replace(targetPath, string.Empty);
                int size = Helper.FileSize(file);
                sw.WriteLine(value + "|" + md5 + "|" + size);
            }
            sw.Close();
            fs.Close();
            AssetDatabase.Refresh();
        }

        public static void Encrypt(string srcFile, string outFile)
        {
            // TODO
        }

        /// <summary>
        /// 宏
        /// </summary>
        private static List<string> defineList;

        static void ParseCSProj(string proj, List<string> blackCspList, ref List<string> csList, ref List<string> dllList)
        {
            List<string> csprojList = new List<string>();

            #region 解析xml

            XmlDocument xml = new XmlDocument();
            xml.Load(proj);
            XmlNode ProjectNode = null;
            foreach (XmlNode x in xml.ChildNodes)
            {
                if (x.Name == "Project")
                {
                    ProjectNode = x;
                    break;
                }
            }

            foreach (XmlNode childNode in ProjectNode.ChildNodes)
            {
                if (childNode.Name == "ItemGroup")
                {
                    foreach (XmlNode item in childNode.ChildNodes)
                    {
                        if (item.Name == "Compile") //cs 引用
                        {
                            var csproj = item.Attributes[0].Value;
                            csList.Add(Helper.NormalizePath(csproj));
                        }
                        else if (item.Name == "Reference") //DLL 引用
                        {
                            var HintPath = item.FirstChild;
                            var dir = HintPath.InnerText.Replace("/", "\\");
                            dllList.Add(Helper.NormalizePath(dir));
                        }
                        else if (item.Name == "ProjectReference") //工程引用
                        {
                            var csproj = item.Attributes[0].Value;
                            csprojList.Add(csproj);
                        }
                    }
                }
                else if (childNode.Name == "PropertyGroup")
                {
                    foreach (XmlNode item in childNode.ChildNodes)
                    {
                        if (item.Name == "DefineConstants")
                        {
                            var define = item.InnerText;

                            var defines = define.Split(';');

                            defineList.AddRange(defines);
                        }
                    }
                }
            }

            #endregion

            //csproj也加入
            foreach (var csproj in csprojList)
            {
                //有editor退出
                if (csproj.ToLower().Contains("editor") || blackCspList.Contains(csproj))
                {
                    continue;
                }

                //
                var gendll = Constants.PROJ_PATH + "/Library/ScriptAssemblies/" + csproj.Replace(".csproj", ".dll");
                if (!File.Exists(gendll))
                {
                    Helper.LogError("不存在:" + gendll);
                }

                dllList?.Add(Helper.NormalizePath(gendll));
            }
        }

        public static bool BuildByRoslyn(string[] dlls, string[] srcs, string output, bool debug = false, bool define = false)
        {
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                for (int i = 0; i < dlls.Length; i++)
                {
                    dlls[i] = dlls[i].Replace("\\", "/");
                }

                for (int i = 0; i < srcs.Length; i++)
                {
                    srcs[i] = srcs[i].Replace("\\", "/");
                }

                output = output.Replace("\\", "/");
            }

            //添加语法树
            var Symbols = defineList;

            List<Microsoft.CodeAnalysis.SyntaxTree> codes = new List<Microsoft.CodeAnalysis.SyntaxTree>();
            CSharpParseOptions opa = null;
            if (define)
            {
                opa = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: Symbols);
            }
            else
            {
                opa = new CSharpParseOptions(LanguageVersion.Latest);
            }

            foreach (var cs in srcs)
            {
                if (!File.Exists(cs))
                    continue;
                var content = File.ReadAllText(cs);
                var syntaxTree = CSharpSyntaxTree.ParseText(content, opa, cs, Encoding.UTF8);
                codes.Add(syntaxTree);
            }

            //添加dll
            List<MetadataReference> assemblies = new List<MetadataReference>();
            foreach (var dll in dlls)
            {
                var metaref = MetadataReference.CreateFromFile(dll);
                if (metaref != null)
                {
                    assemblies.Add(metaref);
                }
            }

            //创建目录
            var dir = Path.GetDirectoryName(output);
            Directory.CreateDirectory(dir);
            //编译参数
            CSharpCompilationOptions option = null;
            if (debug)
            {
                option = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug, warningLevel: 4, allowUnsafe: true);
            }
            else
            {
                option = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, warningLevel: 4, allowUnsafe: true);
            }

            //创建编译器代理
            var assemblyname = Path.GetFileNameWithoutExtension(output);
            var compilation = CSharpCompilation.Create(assemblyname, codes, assemblies, option);
            EmitResult result = null;
            if (!debug)
            {
                result = compilation.Emit(output);
            }
            else
            {
                var pdbPath = output + ".pdb";
                var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: pdbPath);
                using (var dllStream = new MemoryStream())
                using (var pdbStream = new MemoryStream())
                {
                    result = compilation.Emit(dllStream, pdbStream, options: emitOptions);

                    File.WriteAllBytes(output, dllStream.GetBuffer());
                    File.WriteAllBytes(pdbPath, pdbStream.GetBuffer());
                }
            }

            if (!result.Success)
            {
                IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (var diagnostic in failures)
                {
                    var (a, b, c) = ParseRoslynLog(diagnostic.ToString());
                    if (a != null)
                    {
                        Helper.Log(diagnostic.ToString(), a, b, c);
                    }
                    else
                    {
                        UnityEngine.Debug.LogError(diagnostic.ToString());
                    }
                }
            }

            return result.Success;
        }

        static (string, int, int) ParseRoslynLog(string log)
        {
            var part = @"(?<a>.*)\((?<b>\d+),(?<c>\d+)\)";
            var mat = Regex.Match(log, part);
            if (mat.Success)
            {
                var a = mat.Groups["a"].ToString();
                var b = mat.Groups["b"].ToString();
                var c = mat.Groups["c"].ToString();
                return (a, int.Parse(b), int.Parse(c));
            }

            return (null, -1, -1);
        }
    }
}