using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Editor
{
    // Gameビューの解像度を追加する公開APIがないため、内部のGameViewSizesを操作する。
    [InitializeOnLoad]
    public static class GameViewDeviceSizes
    {
        public const string MotoEdge50ProLabel = "moto edge 50 pro";
        public const int MotoEdge50ProWidth = 2712;
        public const int MotoEdge50ProHeight = 1220;

        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Assembly EditorAssembly = typeof(EditorWindow).Assembly;

        static GameViewDeviceSizes()
        {
            if (Application.isBatchMode)
                return;
            EditorApplication.delayCall += RegisterOrWarn;
        }

        public static void Register()
        {
            if (CountRegistered() > 0)
                return;

            var sizes = GetGameViewSizes();
            var sizeType = EditorAssembly.GetType("UnityEditor.GameViewSize", true);
            var sizeKind = EditorAssembly.GetType("UnityEditor.GameViewSizeType", true);
            var size = Activator.CreateInstance(
                sizeType,
                InstanceMembers,
                null,
                new object[]
                {
                    Enum.Parse(sizeKind, "FixedResolution"),
                    MotoEdge50ProWidth,
                    MotoEdge50ProHeight,
                    MotoEdge50ProLabel,
                },
                null
            );
            Invoke(GetAndroidGroup(sizes), "AddCustomSize", size);
            Invoke(sizes, "SaveToHDD");
        }

        public static int CountRegistered()
        {
            var group = GetAndroidGroup(GetGameViewSizes());
            var total = (int)Invoke(group, "GetTotalCount");
            var count = 0;
            for (var i = 0; i < total; i++)
            {
                var size = Invoke(group, "GetGameViewSize", i);
                if (
                    GetValue(size, "sizeType").ToString() == "FixedResolution"
                    && (int)GetValue(size, "width") == MotoEdge50ProWidth
                    && (int)GetValue(size, "height") == MotoEdge50ProHeight
                )
                    count++;
            }
            return count;
        }

        private static void RegisterOrWarn()
        {
            try
            {
                Register();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Gameビューへ{MotoEdge50ProLabel}の解像度を登録できませんでした: {exception}"
                );
            }
        }

        private static object GetGameViewSizes()
        {
            var sizesType = EditorAssembly.GetType("UnityEditor.GameViewSizes", true);
            var instance = typeof(ScriptableSingleton<>)
                .MakeGenericType(sizesType)
                .GetProperty("instance", BindingFlags.Static | BindingFlags.Public);
            if (instance == null)
                throw new MissingMemberException(sizesType.FullName, "instance");
            return instance.GetValue(null);
        }

        private static object GetAndroidGroup(object sizes) =>
            Invoke(sizes, "GetGroup", GameViewSizeGroupType.Android);

        private static object Invoke(object target, string name, params object[] arguments)
        {
            var method = target.GetType().GetMethod(name, InstanceMembers);
            if (method == null)
                throw new MissingMethodException(target.GetType().FullName, name);
            return method.Invoke(target, arguments);
        }

        private static object GetValue(object target, string name)
        {
            var property = target.GetType().GetProperty(name, InstanceMembers);
            if (property == null)
                throw new MissingMemberException(target.GetType().FullName, name);
            return property.GetValue(target);
        }
    }
}
