//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using EP.U3D.EDITOR.BASE;
using ILRManager = EP.U3D.RUNTIME.ILR.ILRManager;

namespace EP.U3D.EDITOR.ILR
{
    public static class MenuILR
    {
        public static List<Type> AdapterTypes = new List<Type>();
        public static Action ILRAfterInit;

        [MenuItem(Constants.MENU_PATCH_BUILD_ILR)]
        public static void BuildILR()
        {
            if (EditorApplication.isCompiling == false)
            {
                ILR.BuildILR.Execute();
            }
            else
            {
                EditorUtility.DisplayDialog("Warning", "Please wait till compile done.", "OK");
            }
        }

        [MenuItem(Constants.MENU_SCRIPT_GEN_ILR_ADAPTERS)]
        public static void GenILRAdapters()
        {
            for (int i = 0; i < AdapterTypes.Count; i++)
            {
                var adapter = AdapterTypes[i];
                var f = Path.Combine(Constants.ILR_ADAPTER_PATH, adapter.Name + "Adapter.cs");
                using (StreamWriter sw = new StreamWriter(f))
                {
                    sw.WriteLine(ILRuntime.Runtime.Enviorment.CrossBindingCodeGenerator.GenerateCrossBindingAdapterCode(adapter, adapter.Namespace));
                }
            }
            // generate register
            string adapterParent = Path.GetFullPath(Constants.ILR_ADAPTER_PATH + "../") + "/";
            string adapterRegister = Path.Combine(adapterParent, "AdapterRegister.cs");
            List<string> css = new List<string>();
            Helper.CollectFiles(adapterParent, css, ".meta");
            if (File.Exists(adapterRegister)) File.Delete(adapterRegister);
            using (var fs = File.Open(adapterRegister, FileMode.CreateNew))
            {
                StreamWriter writer = new StreamWriter(fs);
                writer.WriteLine("// AUTO GENERATED" +
                    ", DO NOT EDIT //");
                writer.WriteLine("public class AdapterRegister");
                writer.WriteLine("{");
                writer.WriteLine("    public static void RegisterCrossBindingAdaptor(ILRuntime.Runtime.Enviorment.AppDomain domain)");
                writer.WriteLine("    {");

                for (int i = 0; i < css.Count; i++)
                {
                    var cs = css[i];
                    if (cs.EndsWith("Adapter.cs"))
                    {
                        var ns = string.Empty;
                        var name = Path.GetFileNameWithoutExtension(cs);
                        var lines = File.ReadAllLines(cs);
                        for (int j = 0; j < lines.Length; j++)
                        {
                            var line = lines[j];
                            if (line.StartsWith("namespace "))
                            {
                                ns = line.Replace("namespace ", "").Replace(" ", "").Replace("{", "").Trim();
                            }
                        }
                        if (string.IsNullOrEmpty(ns))
                        {
                            writer.WriteLine($"        domain.RegisterCrossBindingAdaptor(new {name}());");
                        }
                        else
                        {
                            writer.WriteLine($"        domain.RegisterCrossBindingAdaptor(new {ns}.{name}());");
                        }
                    }
                }

                writer.WriteLine("    }");
                writer.WriteLine("}");
                writer.Close();
                fs.Close();
            }

            AssetDatabase.Refresh();
        }

        [MenuItem(Constants.MENU_SCRIPT_CLEAR_ILR_ADAPTERS)]
        public static void ClearILRAdapters()
        {
            Helper.DeleteDirectory(Constants.ILR_ADAPTER_PATH);
            AssetDatabase.Refresh();
        }

        [MenuItem(Constants.MENU_SCRIPT_GEN_ILR_BINDINGS)]
        public static void GenILRBindings()
        {
            // TODO: 每次编译ILR后是否需要重新生成
            string mainilr = Constants.BUILD_ILR_BUNDLE_PATH + "main" + Constants.ILR_BUNDLE_FILE_EXTENSION;
            ILRManager.Initialize(mainilr, ILRAfterInit);
            try
            {
                ILRuntime.Runtime.CLRBinding.BindingCodeGenerator.GenerateBindingCode(ILRManager.AppDomain, Constants.ILR_BINDING_PATH);
            }
            catch (Exception e)
            {
                Helper.LogError(e);
            }
            ILRManager.Close();
            // TODO: 是否需要检查重复绑定
            var bindingsCS = Path.Combine(Constants.ILR_BINDING_PATH, "CLRBindings.cs");
            if (File.Exists(bindingsCS))
            {
                // make it public
                var ctt = Helper.OpenText(bindingsCS);
                if (!ctt.Contains("public class CLRBindings")) ctt = ctt.Replace("class CLRBindings", "public class CLRBindings");
                Helper.SaveText(bindingsCS, ctt);
            }
            AssetDatabase.Refresh();
        }

        [MenuItem(Constants.MENU_SCRIPT_CLEAR_ILR_BINDINGS)]
        public static void ClearILRBindings()
        {
            Helper.DeleteDirectory(Constants.ILR_BINDING_PATH);
            AssetDatabase.Refresh();
        }
    }
}
