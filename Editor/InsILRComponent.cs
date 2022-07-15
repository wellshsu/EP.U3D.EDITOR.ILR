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
        private readonly Dictionary<string, bool> mArrShow = new Dictionary<string, bool>();
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
            mArrShow.Clear();

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
                            var ffield = type.GetField(field.Key);
                            if (ffield == null) continue;
                            var stype = ffield.FieldType.FullName;
                            var btarray = false;
                            var btlist = false;
                            if (ffield.FieldType.IsArray)
                            {
                                btarray = true;
                                stype = ffield.FieldType.GetElementType().FullName;
                            }
                            else if (ffield.FieldType.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(ffield.FieldType))
                            {
                                // TOFIX[20220707]: script bundle模式下无法获取实际的ilr类型
                                btlist = true;
                                var temp = ffield.FieldType.GetGenericArguments()[0];
                                stype = temp.FullName;
                            }
                            if (btarray || btlist)
                            {
                                if (ffield.GetValue(target) == null) continue;
                                bool sig = mArrShow.TryGetValue(field.Key, out var show);
                                GUILayout.BeginHorizontal();
                                show = EditorGUILayout.Foldout(show, field.Key);
                                if (!sig) mArrShow.Add(field.Key, show);
                                else mArrShow[field.Key] = show;
                                var ocolor = GUI.color;
                                GUI.color = Color.gray;
                                if (GUILayout.Button("-", GUILayout.Width(20), GUILayout.Height(15)))
                                {
                                    var label = GUI.GetNameOfFocusedControl();
                                    var index = -1;
                                    if (!string.IsNullOrEmpty(label))
                                    {
                                        var strs = label.Split('-');
                                        if (strs.Length == 2 && strs[0] == field.Key)
                                        {
                                            int.TryParse(strs[1], out index);
                                        }
                                    }
                                    if (btarray)
                                    {
                                        Array arr = ffield.GetValue(target) as Array;
                                        if (index == -1) index = arr.Length - 1;
                                        if (index >= 0 && index < arr.Length)
                                        {
                                            Array narr;
                                            var ntype = ffield.FieldType.GetElementType();
                                            if (ntype is ILRuntime.Reflection.ILRuntimeType itype)
                                            {
                                                narr = Array.CreateInstance(itype.ILType.TypeForCLR, arr.Length - 1);
                                            }
                                            else
                                            {
                                                narr = Array.CreateInstance(ntype, arr.Length - 1);
                                            }
                                            for (int i = 0; i < arr.Length; i++)
                                            {
                                                if (i == index) continue;
                                                if (arr.GetValue(i) != null)
                                                {
                                                    narr.SetValue(arr.GetValue(i), i < index ? i : i - 1);
                                                }
                                            }
                                            ffield.SetValue(target, narr);
                                        }
                                    }
                                    else
                                    {
                                        object list = ffield.GetValue(target);
                                        PropertyInfo count = list.GetType().GetProperty("Count");
                                        MethodInfo removeAt = list.GetType().GetMethod("RemoveAt");
                                        int ccount = (int)count.GetValue(list);
                                        if (index == -1) index = ccount - 1;
                                        if (index >= 0 && index < ccount) removeAt.Invoke(list, new object[] { index });
                                    }
                                }
                                if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.Height(15)))
                                {
                                    var label = GUI.GetNameOfFocusedControl();
                                    var index = -1;
                                    if (!string.IsNullOrEmpty(label))
                                    {
                                        var strs = label.Split('-');
                                        if (strs.Length == 2 && strs[0] == field.Key)
                                        {
                                            int.TryParse(strs[1], out index);
                                        }
                                    }
                                    if (btarray)
                                    {
                                        Array arr = ffield.GetValue(target) as Array;
                                        if (index == -1) index = arr.Length + 1;
                                        Array narr;
                                        var ntype = ffield.FieldType.GetElementType();
                                        if (ntype is ILRuntime.Reflection.ILRuntimeType itype)
                                        {
                                            narr = Array.CreateInstance(itype.ILType.TypeForCLR, arr.Length + 1);
                                        }
                                        else
                                        {
                                            narr = Array.CreateInstance(ntype, arr.Length + 1);
                                        }
                                        for (int i = 0; i < arr.Length; i++)
                                        {
                                            if (arr.GetValue(i) != null)
                                            {
                                                narr.SetValue(arr.GetValue(i), i < index ? i : i + 1);
                                            }
                                        }
                                        ffield.SetValue(target, narr);
                                    }
                                    else
                                    {
                                        object list = ffield.GetValue(target);
                                        PropertyInfo count = list.GetType().GetProperty("Count");
                                        MethodInfo insert = list.GetType().GetMethod("Insert");
                                        int ccount = (int)count.GetValue(list);
                                        if (index == -1) index = ccount;
                                        insert.Invoke(list, new object[] { index < 0 ? 0 : index, null });
                                    }
                                }
                                GUI.color = ocolor;
                                var olength = 0;
                                if (btarray)
                                {
                                    Array arr = ffield.GetValue(target) as Array;
                                    olength = arr.Length;
                                }
                                else
                                {
                                    object list = ffield.GetValue(target);
                                    PropertyInfo count = list.GetType().GetProperty("Count");
                                    olength = (int)count.GetValue(list);
                                }
                                var length = EditorGUILayout.DelayedIntField(olength, GUILayout.Width(50));
                                GUILayout.EndHorizontal();
                                if (btarray)
                                {
                                    if (length >= 0 && length != olength)
                                    {
                                        Array arr = ffield.GetValue(target) as Array;
                                        Array narr;
                                        var ntype = ffield.FieldType.GetElementType();
                                        if (ntype is ILRuntime.Reflection.ILRuntimeType itype)
                                        {
                                            narr = Array.CreateInstance(itype.ILType.TypeForCLR, length);
                                        }
                                        else
                                        {
                                            narr = Array.CreateInstance(ntype, length);
                                        }
                                        for (int j = 0; j < length; j++)
                                        {
                                            if (arr.Length > j)
                                            {
                                                if (arr.GetValue(j) != null)
                                                {
                                                    narr.SetValue(arr.GetValue(j), j);
                                                }
                                            }
                                        }
                                        ffield.SetValue(target, narr);
                                    }
                                    if (show)
                                    {
                                        Array arr = ffield.GetValue(target) as Array;
                                        if (arr.Length == 0)
                                        {
                                            EditorGUILayout.HelpBox("List is empty.", MessageType.Info);
                                        }
                                        for (int i = 0; i < arr.Length; i++)
                                        {
                                            DrawFieldInRuntime("Element " + i, stype, arr.GetValue(i), out object fvalue, field.Key + "-" + i);
                                            arr.SetValue(fvalue, i);
                                        }
                                    }
                                }
                                else
                                {
                                    if (length >= 0 && length != olength)
                                    {
                                        object list = ffield.GetValue(target);
                                        object nlist;
                                        Type ltype = typeof(List<>);
                                        var ntype = ffield.FieldType.GetGenericArguments()[0];
                                        if (ntype is ILRuntime.Reflection.ILRuntimeType itype)
                                        {
                                            Type nntype = ltype.MakeGenericType(new Type[] { itype.ILType.TypeForCLR });
                                            nlist = Activator.CreateInstance(nntype);
                                        }
                                        else
                                        {
                                            Type nntype = ltype.MakeGenericType(new Type[] { ntype });
                                            nlist = Activator.CreateInstance(nntype);
                                        }
                                        MethodInfo toArray = list.GetType().GetMethod("ToArray");
                                        MethodInfo add = nlist.GetType().GetMethod("Add");
                                        Array arr = toArray.Invoke(list, null) as Array;
                                        for (int j = 0; j < length; j++)
                                        {
                                            if (arr.Length > j)
                                            {
                                                add.Invoke(nlist, new object[] { arr.GetValue(j) });
                                            }
                                        }
                                        ffield.SetValue(target, nlist);
                                    }
                                    if (show)
                                    {
                                        object list = ffield.GetValue(target);
                                        PropertyInfo count = list.GetType().GetProperty("Count");
                                        int ccount = (int)count.GetValue(list);
                                        if (ccount == 0)
                                        {
                                            EditorGUILayout.HelpBox("List is empty.", MessageType.Info);
                                        }
                                        MethodInfo toArray = list.GetType().GetMethod("ToArray");
                                        MethodInfo clear = list.GetType().GetMethod("Clear");
                                        MethodInfo addRange = list.GetType().GetMethod("AddRange");
                                        Array arr = toArray.Invoke(list, null) as Array;
                                        for (int i = 0; i < arr.Length; i++)
                                        {
                                            DrawFieldInRuntime("Element " + i, stype, arr.GetValue(i), out object fvalue, field.Key + "-" + i);
                                            arr.SetValue(fvalue, i);
                                        }
                                        clear.Invoke(list, null);
                                        addRange.Invoke(list, new object[] { arr });
                                    }
                                }
                            }
                            else
                            {
                                try
                                {
                                    DrawFieldInRuntime(field.Key, field.Value, ffield.GetValue(target), out object fvalue);
                                    ffield.SetValue(target, fvalue);
                                }
                                catch (Exception)
                                {
                                    Helper.LogError(field.Key);
                                    throw;
                                }

                            }
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
                        object fobject = null; // 用于获取字段初始值
                        foreach (var ffield in ffields)
                        {
                            if (ffield.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
                            if (ffield.IsPrivate && ffield.GetCustomAttribute<SerializeField>() == null) continue;
                            if (mReflects.ContainsKey(ffield.Name) == false)
                            {
                                mReflects.Add(ffield.Name, ffield.FieldType.FullName);
                                var ret = mInstance.Fields.Find((ele) => { return ele.Key == ffield.Name; });
                                var isnew = false;
                                if (ret == null)
                                {
                                    ret = new ILRComponent.Field();
                                    ret.Key = ffield.Name;
                                    mInstance.Fields.Add(ret);
                                    isnew = true;
                                }
                                var type = ffield.FieldType.FullName;
                                var btarray = false;
                                var btlist = false;
                                var blbvalue = false;
                                if (ffield.FieldType.IsArray)
                                {
                                    btarray = true;
                                    type = ffield.FieldType.GetElementType().FullName;
                                    var temp = ffield.FieldType.Assembly.GetType(type);
                                    blbvalue = temp.IsEnum;
                                }
                                else if (ffield.FieldType.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(ffield.FieldType))
                                {
                                    btlist = true;
                                    var temp = ffield.FieldType.GetGenericArguments()[0];
                                    type = temp.FullName;
                                    blbvalue = temp.IsEnum;
                                }
                                if (type != ret.Type) ret.Reset();
                                if (blbvalue == false)
                                {
                                    blbvalue = type == "System.Int32" || type == "System.Int64" || type == "System.Single" ||
                                         type == "System.Double" || type == "System.Boolean" || type == "UnityEngine.Vector2" ||
                                         type == "UnityEngine.Vector3" || type == "UnityEngine.Vector4" || type == "UnityEngine.Color" ||
                                         type == "System.String" || ffield.FieldType.IsEnum;
                                }
                                ret.Type = type;
                                ret.BTArray = btarray;
                                ret.BTList = btlist;
                                ret.BLBValue = blbvalue;
                                // [20220715]: 字段初始化赋值
                                if (isnew && !btarray && !btlist)
                                {
                                    if (fobject == null) fobject = Activator.CreateInstance(mType);
                                    if (fobject != null)
                                    {
                                        if (type == "System.Int32")
                                        {
                                            ret.BValue = BitConverter.GetBytes((int)ffield.GetValue(fobject));
                                        }
                                        else if (type == "System.Int64")
                                        {
                                            ret.BValue = BitConverter.GetBytes((long)ffield.GetValue(fobject));
                                        }
                                        else if (type == "System.Single")
                                        {
                                            ret.BValue = BitConverter.GetBytes((float)ffield.GetValue(fobject));
                                        }
                                        else if (type == "System.Double")
                                        {
                                            ret.BValue = BitConverter.GetBytes((double)ffield.GetValue(fobject));
                                        }
                                        else if (type == "System.Boolean")
                                        {
                                            ret.BValue = BitConverter.GetBytes((bool)ffield.GetValue(fobject));
                                        }
                                        else if (type == "UnityEngine.Vector2")
                                        {
                                            ret.BValue = Helper.StructToByte((Vector2)ffield.GetValue(fobject));
                                        }
                                        else if (type == "UnityEngine.Vector3")
                                        {
                                            ret.BValue = Helper.StructToByte((Vector3)ffield.GetValue(fobject));
                                        }
                                        else if (type == "UnityEngine.Vector4")
                                        {
                                            ret.BValue = Helper.StructToByte((Vector4)ffield.GetValue(fobject));
                                        }
                                        else if (type == "UnityEngine.Color")
                                        {
                                            ret.BValue = Helper.StructToByte((Color)ffield.GetValue(fobject));
                                        }
                                        else if (type == "System.String")
                                        {
                                            ret.BValue = Encoding.UTF8.GetBytes((string)ffield.GetValue(fobject));
                                        }
                                        else if (ffield.FieldType.IsEnum)
                                        {
                                            ret.BValue = BitConverter.GetBytes((int)ffield.GetValue(fobject));
                                        }
                                    }
                                }
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
                                if (field.BTArray || field.BTList)
                                {
                                    if (field.BLBValue)
                                    {
                                        if (field.LBValue == null) field.LBValue = new List<ILRComponent.Byte>();
                                    }
                                    else
                                    {
                                        if (field.LOValue == null) field.LOValue = new List<UnityEngine.Object>();
                                    }
                                    GUILayout.BeginHorizontal();
                                    field.BLShow = EditorGUILayout.Foldout(field.BLShow, field.Key);
                                    var ocolor = GUI.color;
                                    GUI.color = Color.gray;
                                    if (GUILayout.Button("-", GUILayout.Width(20), GUILayout.Height(15)))
                                    {
                                        var label = GUI.GetNameOfFocusedControl();
                                        var index = -1;
                                        if (!string.IsNullOrEmpty(label))
                                        {
                                            var strs = label.Split('-');
                                            if (strs.Length == 2 && strs[0] == field.Key)
                                            {
                                                int.TryParse(strs[1], out index);
                                            }
                                        }
                                        if (field.BLBValue)
                                        {
                                            if (index == -1) index = field.LBValue.Count - 1;
                                            if (index >= 0 && index < field.LBValue.Count) field.LBValue.RemoveAt(index);
                                        }
                                        else
                                        {
                                            if (index == -1) index = field.LOValue.Count - 1;
                                            if (index >= 0 && index < field.LOValue.Count) field.LOValue.RemoveAt(index);
                                        }
                                    }
                                    if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.Height(15)))
                                    {
                                        var label = GUI.GetNameOfFocusedControl();
                                        var index = -1;
                                        if (!string.IsNullOrEmpty(label))
                                        {
                                            var strs = label.Split('-');
                                            if (strs.Length == 2 && strs[0] == field.Key)
                                            {
                                                int.TryParse(strs[1], out index);
                                            }
                                        }
                                        if (field.BLBValue)
                                        {
                                            if (index == -1) index = field.LBValue.Count;
                                            field.LBValue.Insert(index < 0 ? 0 : index, new ILRComponent.Byte(new byte[16]));
                                        }
                                        else
                                        {
                                            if (index == -1) index = field.LOValue.Count;
                                            field.LOValue.Insert(index < 0 ? 0 : index, null);
                                        }
                                    }
                                    GUI.color = ocolor;
                                    var length = field.BLBValue ?
                                        EditorGUILayout.DelayedIntField(field.LBValue.Count, GUILayout.Width(50)) :
                                        EditorGUILayout.DelayedIntField(field.LOValue.Count, GUILayout.Width(50));
                                    GUILayout.EndHorizontal();
                                    if (field.BLBValue)
                                    {
                                        if (length >= 0)
                                        {
                                            if (length != field.LBValue.Count)
                                            {
                                                var narr = new List<ILRComponent.Byte>(length);
                                                for (int j = 0; j < length; j++)
                                                {
                                                    if (field.LBValue.Count > j) narr.Add(field.LBValue[j]);
                                                    else narr.Add(new ILRComponent.Byte(new byte[16]));
                                                }
                                                field.LBValue = narr;
                                            }
                                        }
                                        if (field.BLShow)
                                        {
                                            if (field.LBValue.Count == 0)
                                            {
                                                EditorGUILayout.HelpBox("List is empty.", MessageType.Info);
                                            }
                                            for (int j = 0; j < field.LBValue.Count; j++)
                                            {
                                                var bvalue = field.LBValue[j];
                                                UnityEngine.Object ovalue = null;
                                                DrawFieldInEditor("Element " + j, field.Type, ref bvalue.Data, ref ovalue, field.Key + "-" + j);
                                                field.LBValue[j] = bvalue;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (length >= 0)
                                        {
                                            if (length != field.LOValue.Count)
                                            {
                                                var narr = new List<UnityEngine.Object>(length);
                                                for (int j = 0; j < length; j++)
                                                {
                                                    if (field.LOValue.Count > j) narr.Add(field.LOValue[j]);
                                                    else narr.Add(null);
                                                }
                                                field.LOValue = narr;
                                            }
                                        }
                                        if (field.BLShow)
                                        {
                                            if (field.LOValue.Count == 0)
                                            {
                                                EditorGUILayout.HelpBox("List is empty.", MessageType.Info);
                                            }
                                            for (int j = 0; j < field.LOValue.Count; j++)
                                            {
                                                byte[] bvalue = null;
                                                UnityEngine.Object ovalue = field.LOValue[j];
                                                DrawFieldInEditor("Element " + j, field.Type, ref bvalue, ref ovalue, field.Key + "-" + j);
                                                field.LOValue[j] = ovalue;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    DrawFieldInEditor(field.Key, field.Type, ref field.BValue, ref field.OValue);
                                }
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

        private void DrawFieldInRuntime(string key, string type, object ivalue, out object fvalue, string cname = "")
        {
            fvalue = null;
            GUILayout.BeginHorizontal();
            if (string.IsNullOrEmpty(cname) == false) GUI.SetNextControlName(cname);
            if (type == "System.Int32")
            {
                int v = (int)ivalue;
                fvalue = EditorGUILayout.IntField(key, v);
            }
            else if (type == "System.Int64")
            {
                long v = (long)ivalue;
                fvalue = EditorGUILayout.LongField(key, v);
            }
            else if (type == "System.Single")
            {
                float v = (float)ivalue;
                fvalue = EditorGUILayout.FloatField(key, v);
            }
            else if (type == "System.Double")
            {
                double v = (double)ivalue;
                fvalue = EditorGUILayout.DoubleField(key, v);
            }
            else if (type == "System.Boolean")
            {
                bool v = (bool)ivalue;
                fvalue = EditorGUILayout.Toggle(key, v);
            }
            else if (type == "UnityEngine.Vector2")
            {
                Vector2 v = (Vector2)ivalue;
                fvalue = EditorGUILayout.Vector2Field(key, v);
            }
            else if (type == "UnityEngine.Vector3")
            {
                Vector3 v = (Vector3)ivalue;
                fvalue = EditorGUILayout.Vector3Field(key, v);
            }
            else if (type == "UnityEngine.Vector4")
            {
                Vector4 v = (Vector4)ivalue;
                fvalue = EditorGUILayout.Vector4Field(key, v);
            }
            else if (type == "UnityEngine.Color")
            {
                Color v = (Color)ivalue;
                fvalue = EditorGUILayout.ColorField(key, v);
            }
            else if (type == "System.String")
            {
                string v = (string)ivalue;
                fvalue = EditorGUILayout.TextField(key, v);
            }
            else
            {
                Type ftype = null;
                for (int i = 0; i < Constants.COMPONENT_REFLECT_DLLS.Count; i++)
                {
                    var dll = Constants.COMPONENT_REFLECT_DLLS[i];
                    if (dll != null)
                    {
                        var t = dll.GetType(type);
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
                        UnityEngine.Object v = (UnityEngine.Object)ivalue;
                        fvalue = EditorGUILayout.ObjectField(key, v, ftype, true);
                    }
                    else if (ftype.IsEnum)
                    {
                        var enums = Enum.GetValues(ftype);
                        var c = (int)ivalue;
                        Enum v = (Enum)enums.GetValue(0);
                        for (int j = 0; j < enums.Length; j++)
                        {
                            var e = enums.GetValue(j);
                            if ((int)e == c)
                            {
                                v = (Enum)e;
                            }
                        }
                        fvalue = EditorGUILayout.EnumPopup(key, v);
                    }
                    else if (ftype.IsSubclassOf(typeof(IILRComponent)))
                    {
                        IILRComponent v = (IILRComponent)ivalue;
                        ILRComponent lv = null;
                        if (v != null) lv = v.gameObject.GetComponent<ILRComponent>();
                        lv = EditorGUILayout.ObjectField(key, lv, typeof(ILRComponent), true) as ILRComponent;
                        if (lv)
                        {
                            var vs = lv.GetComponents<ILRComponent>();
                            for (int i = 0; i < vs.Length; i++)
                            {
                                var t = vs[i];
                                if (t.FullName == type)
                                {
                                    fvalue = t.Object;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawFieldInEditor(string key, string type, ref byte[] bvalue, ref UnityEngine.Object ovalue, string cname = "")
        {
            GUILayout.BeginHorizontal();
            if (string.IsNullOrEmpty(cname) == false) GUI.SetNextControlName(cname);
            if (type == "System.Int32")
            {
                int v = BitConverter.ToInt32(bvalue, 0);
                v = EditorGUILayout.IntField(key, v);
                bvalue = BitConverter.GetBytes(v);
            }
            else if (type == "System.Int64")
            {
                long v = BitConverter.ToInt64(bvalue, 0);
                v = EditorGUILayout.LongField(key, v);
                bvalue = BitConverter.GetBytes(v);
            }
            else if (type == "System.Single")
            {
                float v = BitConverter.ToSingle(bvalue, 0);
                v = EditorGUILayout.FloatField(key, v);
                bvalue = BitConverter.GetBytes(v);
            }
            else if (type == "System.Double")
            {
                double v = BitConverter.ToDouble(bvalue, 0);
                v = EditorGUILayout.DoubleField(key, v);
                bvalue = BitConverter.GetBytes(v);
            }
            else if (type == "System.Boolean")
            {
                bool v = BitConverter.ToBoolean(bvalue, 0);
                v = EditorGUILayout.Toggle(key, v);
                bvalue = BitConverter.GetBytes(v);
            }
            else if (type == "UnityEngine.Vector2")
            {
                Vector2 v = Helper.ByteToStruct<Vector2>(bvalue);
                v = EditorGUILayout.Vector2Field(key, v);
                bvalue = Helper.StructToByte(v);
            }
            else if (type == "UnityEngine.Vector3")
            {
                Vector3 v = Helper.ByteToStruct<Vector3>(bvalue);
                v = EditorGUILayout.Vector3Field(key, v);
                bvalue = Helper.StructToByte(v);
            }
            else if (type == "UnityEngine.Vector4")
            {
                Vector4 v = Helper.ByteToStruct<Vector4>(bvalue);
                v = EditorGUILayout.Vector4Field(key, v);
                bvalue = Helper.StructToByte(v);
            }
            else if (type == "UnityEngine.Color")
            {
                Color v = Helper.ByteToStruct<Color>(bvalue);
                v = EditorGUILayout.ColorField(key, v);
                bvalue = Helper.StructToByte(v);
            }
            else if (type == "System.String")
            {
                string v = Encoding.UTF8.GetString(bvalue);
                v = EditorGUILayout.TextField(key, v);
                bvalue = Encoding.UTF8.GetBytes(v);
            }
            else
            {
                Type ftype = null;
                for (int j = 0; j < Constants.COMPONENT_REFLECT_DLLS.Count; j++)
                {
                    var dll = Constants.COMPONENT_REFLECT_DLLS[j];
                    if (dll != null)
                    {
                        var t = dll.GetType(type);
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
                        ovalue = EditorGUILayout.ObjectField(key, ovalue, ftype, true);
                    }
                    else if (ftype.IsEnum)
                    {
                        var enums = Enum.GetValues(ftype);
                        var c = BitConverter.ToInt32(bvalue, 0);
                        Enum v = (Enum)enums.GetValue(0);
                        for (int j = 0; j < enums.Length; j++)
                        {
                            var e = enums.GetValue(j);
                            if ((int)e == c)
                            {
                                v = (Enum)e;
                            }
                        }
                        v = EditorGUILayout.EnumPopup(key, v);
                        bvalue = BitConverter.GetBytes((int)(object)v);
                    }
                    else if (ftype.IsSubclassOf(typeof(IILRComponent)))
                    {
                        ILRComponent v = ovalue as ILRComponent;
                        v = EditorGUILayout.ObjectField(key, v, typeof(ILRComponent), true) as ILRComponent;
                        bool sig = false;
                        if (v)
                        {
                            var vs = v.GetComponents<ILRComponent>();
                            for (int i = 0; i < vs.Length; i++)
                            {
                                var t = vs[i];
                                if (t.FullName == type)
                                {
                                    sig = true;
                                    ovalue = t;
                                    break;
                                }
                            }
                        }
                        if (!sig) ovalue = null;
                    }
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}