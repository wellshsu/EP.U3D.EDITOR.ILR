//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;
using System;
using System.Text;
using EP.U3D.RUNTIME.ILR;
using EP.U3D.EDITOR.BASE;

namespace EP.U3D.EDITOR.ILR
{
    [CustomEditor(typeof(ILRComponent))]
    [CanEditMultipleObjects]
    public class InsILRComponent : Editor
    {
        private ILRComponent mInstance;
        public string[] mILRScripts;
        private string mILRProjRoot;
        private int mSelectedScript = -1;
        private int mLastSelectedScript = -1;
        private string mSearchText;
        private Type mType;
        private readonly Dictionary<string, string> mReflects = new Dictionary<string, string>();
        private readonly string mHelpText =
            "[Note] USE AssetManager.LoadAsset/LoadScene to load prefab.\n" +
            "USE UIHelper.AddComponent to add component dynamicly.\n";

        private void OnEnable()
        {
            mInstance = target as ILRComponent;
            mILRProjRoot = Constants.ILR_SCRIPT_WORKSPACE;
            List<string> scripts = new List<string>();
            CollectScripts(mILRProjRoot, scripts);
            mILRScripts = scripts.ToArray();

            mSelectedScript = -1;
            mLastSelectedScript = -1;
            mType = null;
            mReflects.Clear();

            if (string.IsNullOrEmpty(mInstance.FilePath) == false)
            {
                for (int i = 0; i < mILRScripts.Length; i++)
                {
                    string str = mILRScripts[i];
                    if (mInstance.FilePath.EndsWith(str))
                    {
                        mSelectedScript = i;
                        break;
                    }
                }
            }
            else if (string.IsNullOrEmpty(mInstance.FullName) == false) // 通过动态添加的
            {
                string ns = string.Empty;
                string name = string.Empty;
                int idx = mInstance.FullName.LastIndexOf(".");
                if (idx > 0)
                {
                    ns = mInstance.FullName.Substring(0, idx);
                    name = mInstance.FullName.Substring(idx + 1);
                }
                else
                {
                    name = mInstance.FullName;
                }
                foreach (var src in mILRScripts)
                {
                    var content = Helper.OpenText(Path.Combine(mILRProjRoot, src));
                    if (content.Contains($"class {name} ") &&
                       (string.IsNullOrEmpty(ns) || content.Contains($"namespace {ns}")))
                    {
                        mInstance.FilePath = src;
                        break;
                    }
                }
                for (int i = 0; i < mILRScripts.Length; i++)
                {
                    string str = mILRScripts[i];
                    if (mInstance.FilePath.EndsWith(str))
                    {
                        mSelectedScript = i;
                        break;
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            if (mInstance == null) return;
            GUILayout.Space(10f);
            EditorGUILayout.HelpBox(mHelpText, MessageType.Info);

            if (Application.isPlaying)
            {
                if (mSelectedScript != -1)
                {
                    string path = mILRScripts[mSelectedScript];
                    path = mILRProjRoot + path;

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.IntPopup(mSelectedScript, mILRScripts, null, GUILayout.Height(15));
                    if (GUILayout.Button(new GUIContent("Edit"), GUILayout.Height(17), GUILayout.Width(50)))
                    {
                        EditorIDE.OpenScriptAtLine(path, 1);
                    }
                    GUILayout.EndHorizontal();
                    if (mInstance.Object == null)
                    {
                        EditorGUILayout.HelpBox($"ILR Object of {mInstance.FullName} is nil", MessageType.Error);
                    }
                    else
                    {
                        mReflects.Clear();
                        var target = mInstance.Object;
                        var type = mInstance.Type;
                        var ffields = type.GetFields();
                        foreach (var ffield in ffields)
                        {
                            if (ffield.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
                            if (ffield.IsPrivate && ffield.GetCustomAttribute<SerializeField>() == null) continue;
                            if (mReflects.ContainsKey(ffield.Name) == false) mReflects.Add(ffield.Name, ffield.FieldType.FullName);
                        }
                        foreach (var field in mReflects)
                        {
                            GUILayout.BeginHorizontal();
                            var ffield = type.GetField(field.Key);
                            if (ffield == null) continue;
                            if (field.Value == "System.Int32")
                            {
                                int v = (int)ffield.GetValue(target);
                                v = EditorGUILayout.IntField(field.Key, v);
                                ffield.SetValue(target, v);
                            }
                            else if (field.Value == "System.Int64")
                            {
                                long v = (long)ffield.GetValue(target);
                                v = EditorGUILayout.LongField(field.Key, v);
                                ffield.SetValue(target, v);
                            }
                            else if (field.Value == "System.Single")
                            {
                                float v = (float)ffield.GetValue(target);
                                v = EditorGUILayout.FloatField(field.Key, v);
                                ffield.SetValue(target, v);
                            }
                            else if (field.Value == "System.Double")
                            {
                                double v = (double)ffield.GetValue(target);
                                v = EditorGUILayout.DoubleField(field.Key, v);
                                ffield.SetValue(target, v);
                            }
                            else if (field.Value == "System.Boolean")
                            {
                                bool v = (bool)ffield.GetValue(target);
                                v = EditorGUILayout.Toggle(field.Key, v);
                                ffield.SetValue(target, v);
                            }
                            else if (field.Value == "UnityEngine.Vector2")
                            {
                                Vector2 v = (Vector2)ffield.GetValue(target);
                                v = EditorGUILayout.Vector2Field(field.Key, v);
                                ffield.SetValue(target, v);
                            }
                            else if (field.Value == "UnityEngine.Vector3")
                            {
                                Vector3 v = (Vector3)ffield.GetValue(target);
                                v = EditorGUILayout.Vector3Field(field.Key, v);
                                ffield.SetValue(target, v);
                            }
                            else if (field.Value == "UnityEngine.Vector4")
                            {
                                Vector4 v = (Vector4)ffield.GetValue(target);
                                v = EditorGUILayout.Vector4Field(field.Key, v);
                                ffield.SetValue(target, v);
                            }
                            else if (field.Value == "UnityEngine.Color")
                            {
                                Color v = (Color)ffield.GetValue(target);
                                v = EditorGUILayout.ColorField(field.Key, v);
                                ffield.SetValue(target, v);
                            }
                            else if (field.Value == "System.String")
                            {
                                string v = (string)ffield.GetValue(target);
                                v = EditorGUILayout.TextField(field.Key, v);
                                ffield.SetValue(target, v);
                            }
                            else
                            {
                                Type ftype = null;
                                for (int i = 0; i < Constants.COMPONENT_REFLECT_DLLS.Count; i++)
                                {
                                    var dll = Constants.COMPONENT_REFLECT_DLLS[i];
                                    if (dll != null)
                                    {
                                        var t = dll.GetType(field.Value);
                                        if (t != null)
                                        {
                                            ftype = t;
                                            break;
                                        }
                                    }
                                }
                                if (ftype != null)
                                {
                                    if (ftype.IsSubclassOf(typeof(UnityEngine.Object)))
                                    {
                                        UnityEngine.Object v = (UnityEngine.Object)ffield.GetValue(target);
                                        v = EditorGUILayout.ObjectField(field.Key, v, ftype, true);
                                        ffield.SetValue(target, v);
                                    }
                                    else if (ftype.IsSubclassOf(typeof(IILRComponent)))
                                    {
                                        IILRComponent v = (IILRComponent)ffield.GetValue(target);
                                        ILRComponent lv = null;
                                        if (v != null)
                                        {
                                            lv = v.gameObject.GetComponent<ILRComponent>();
                                            if (lv)
                                            {
                                                lv = EditorGUILayout.ObjectField(field.Key, lv, typeof(ILRComponent), true) as ILRComponent;
                                                if (lv && lv.FullName == field.Value)
                                                {
                                                    ffield.SetValue(target, lv.Object);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                }
            }
            else
            {
                mLastSelectedScript = mSelectedScript;
                mSearchText = EditorGUILayout.TextField("Search Script", mSearchText);
                if (!string.IsNullOrEmpty(mSearchText))
                {
                    for (int i = 0; i < mILRScripts.Length; i++)
                    {
                        var str = mILRScripts[i];
                        if (str.IndexOf(mSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (GUILayout.Button(new GUIContent(str)))
                            {
                                mSelectedScript = i;
                                mSearchText = "";
                            }
                        }
                    }
                }
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                mSelectedScript = EditorGUILayout.IntPopup(mSelectedScript, mILRScripts, null, GUILayout.Height(15));
                if (GUILayout.Button(new GUIContent("Edit"), GUILayout.Height(17), GUILayout.Width(50)))
                {
                    string path = mILRScripts[mSelectedScript];
                    path = mILRProjRoot + path;
                    EditorIDE.OpenScriptAtLine(path, 1);
                }
                GUILayout.EndHorizontal();
                if (mSelectedScript == -1)
                {
                    EditorGUILayout.HelpBox("Please select a script or remove this component.", MessageType.Error);
                }
                else
                {
                    if (mSelectedScript != mLastSelectedScript)
                    {
                        mType = null;
                        mInstance.FilePath = mILRScripts[mSelectedScript];
                    }
                    if (mType == null)
                    {
                        var content = Helper.OpenText(Path.Combine(mILRProjRoot, mInstance.FilePath));
                        for (int i = 0; i < Constants.COMPONENT_REFLECT_DLLS.Count; i++)
                        {
                            var dll = Constants.COMPONENT_REFLECT_DLLS[i];
                            if (dll != null)
                            {
                                var types = dll.GetTypes();
                                foreach (var t in types)
                                {
                                    if (content.Contains($"class {t.Name} ") &&
                                        (string.IsNullOrEmpty(t.Namespace) || content.Contains($"namespace {t.Namespace}")))
                                    {
                                        mType = t;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (mType == null)
                    {
                        EditorGUILayout.HelpBox(Helper.StringFormat("Can not reflect type of {0} in any dlls.", mInstance.FilePath), MessageType.Error);
                    }
                    else
                    {
                        mReflects.Clear();
                        mInstance.FullName = mType.FullName;
                        var ffields = mType.GetFields();
                        foreach (var ffield in ffields)
                        {
                            if (ffield.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
                            if (ffield.IsPrivate && ffield.GetCustomAttribute<SerializeField>() == null) continue;
                            if (mReflects.ContainsKey(ffield.Name) == false)
                            {
                                mReflects.Add(ffield.Name, ffield.FieldType.FullName);
                                var ret = mInstance.Fields.Find((ele) => { return ele.Key == ffield.Name; });
                                if (ret == null)
                                {
                                    ret = new ILRComponent.Field();
                                    ret.Key = ffield.Name;
                                    mInstance.Fields.Add(ret);
                                }
                                if (ffield.FieldType.FullName != ret.Type) ret.Reset();
                                ret.Type = ffield.FieldType.FullName;
                            }
                        }
                        for (int i = 0; i < mInstance.Fields.Count;)
                        {
                            ILRComponent.Field field = mInstance.Fields[i];
                            if (mReflects.ContainsKey(field.Key) == false)
                            {
                                mInstance.Fields.Remove(field);
                            }
                            else
                            {
                                i++;
                                GUILayout.BeginHorizontal();
                                if (field.Type == "System.Int32")
                                {
                                    int v = BitConverter.ToInt32(field.BValue, 0);
                                    v = EditorGUILayout.IntField(field.Key, v);
                                    field.BValue = BitConverter.GetBytes(v);
                                }
                                else if (field.Type == "System.Int64")
                                {
                                    long v = BitConverter.ToInt64(field.BValue, 0);
                                    v = EditorGUILayout.LongField(field.Key, v);
                                    field.BValue = BitConverter.GetBytes(v);
                                }
                                else if (field.Type == "System.Single")
                                {
                                    float v = BitConverter.ToSingle(field.BValue, 0);
                                    v = EditorGUILayout.FloatField(field.Key, v);
                                    field.BValue = BitConverter.GetBytes(v);
                                }
                                else if (field.Type == "System.Double")
                                {
                                    double v = BitConverter.ToDouble(field.BValue, 0);
                                    v = EditorGUILayout.DoubleField(field.Key, v);
                                    field.BValue = BitConverter.GetBytes(v);
                                }
                                else if (field.Type == "System.Boolean")
                                {
                                    bool v = BitConverter.ToBoolean(field.BValue, 0);
                                    v = EditorGUILayout.Toggle(field.Key, v);
                                    field.BValue = BitConverter.GetBytes(v);
                                }
                                else if (field.Type == "UnityEngine.Vector2")
                                {
                                    Vector2 v = Helper.ByteToStruct<Vector2>(field.BValue);
                                    v = EditorGUILayout.Vector2Field(field.Key, v);
                                    field.BValue = Helper.StructToByte(v);
                                }
                                else if (field.Type == "UnityEngine.Vector3")
                                {
                                    Vector3 v = Helper.ByteToStruct<Vector3>(field.BValue);
                                    v = EditorGUILayout.Vector3Field(field.Key, v);
                                    field.BValue = Helper.StructToByte(v);
                                }
                                else if (field.Type == "UnityEngine.Vector4")
                                {
                                    Vector4 v = Helper.ByteToStruct<Vector4>(field.BValue);
                                    v = EditorGUILayout.Vector4Field(field.Key, v);
                                    field.BValue = Helper.StructToByte(v);
                                }
                                else if (field.Type == "UnityEngine.Color")
                                {
                                    Color v = Helper.ByteToStruct<Color>(field.BValue);
                                    v = EditorGUILayout.ColorField(field.Key, v);
                                    field.BValue = Helper.StructToByte(v);
                                }
                                else if (field.Type == "System.String")
                                {
                                    string v = Encoding.UTF8.GetString(field.BValue);
                                    v = EditorGUILayout.TextField(field.Key, v);
                                    field.BValue = Encoding.UTF8.GetBytes(v);
                                }
                                else
                                {
                                    Type ftype = null;
                                    for (int j = 0; j < Constants.COMPONENT_REFLECT_DLLS.Count; j++)
                                    {
                                        var dll = Constants.COMPONENT_REFLECT_DLLS[j];
                                        if (dll != null)
                                        {
                                            var t = dll.GetType(field.Type);
                                            if (t != null)
                                            {
                                                ftype = t;
                                                break;
                                            }
                                        }
                                    }
                                    if (ftype != null)
                                    {
                                        if (ftype.IsSubclassOf(typeof(UnityEngine.Object)))
                                        {
                                            field.OValue = EditorGUILayout.ObjectField(field.Key, field.OValue, ftype, true);
                                        }
                                        else if (ftype.IsSubclassOf(typeof(IILRComponent)))
                                        {
                                            ILRComponent v = field.OValue as ILRComponent;
                                            v = EditorGUILayout.ObjectField(field.Key, v, typeof(ILRComponent), true) as ILRComponent;
                                            if (v && v.FullName == field.Type)
                                            {
                                                field.OValue = v;
                                            }
                                            else
                                            {
                                                field.OValue = null;
                                            }
                                        }
                                    }
                                }
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                }
                if (GUI.changed) EditorUtility.SetDirty(target);
            }
        }

        private void CollectScripts(string directory, List<string> outfiles)
        {
            if (Directory.Exists(directory))
            {
                string[] files = Directory.GetFiles(directory);
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    file = file.Replace("\\", "/");
                    if (file.EndsWith(".cs"))
                    {
                        file = file.Substring(mILRProjRoot.Length);
                        outfiles.Add(file);
                    }
                }
                string[] dirs = Directory.GetDirectories(directory);
                for (int i = 0; i < dirs.Length; i++)
                {
                    CollectScripts(dirs[i], outfiles);
                }
            }
            else if (File.Exists(directory))
            {
                directory = directory.Substring(mILRProjRoot.Length);
                outfiles.Add(directory);
            }
        }
    }
}