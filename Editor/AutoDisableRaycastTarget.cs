using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RuriKit.Editor
{
    /// <summary>
    ///     在编辑器中添加 UI 组件时，自动关闭非交互图形组件的射线检测，并保留
    ///     <see cref="Selectable.targetGraphic" /> 使用的图形组件的射线检测。
    /// </summary>
    [InitializeOnLoad]
    public static class AutoDisableRaycastTarget
    {
        static AutoDisableRaycastTarget()
        {
            ObjectFactory.componentWasAdded += OnComponentWasAdded;
        }

        private static void OnComponentWasAdded(Component component)
        {
            switch (component)
            {
                case Graphic graphic:
                {
                    Selectable[] selectables = graphic.gameObject.GetComponents<Selectable>();
                    bool isTargetGraphic = false;

                    foreach (Selectable s in selectables)
                    {
                        if (s.targetGraphic == graphic)
                        {
                            isTargetGraphic = true;
                            break;
                        }
                    }

                    if (!isTargetGraphic)
                    {
                        graphic.raycastTarget = false;
                    }

                    break;
                }
                case Selectable selectable:
                {
                    if (selectable.targetGraphic)
                    {
                        selectable.targetGraphic.raycastTarget = true;
                    }

                    break;
                }
            }
        }
    }
}