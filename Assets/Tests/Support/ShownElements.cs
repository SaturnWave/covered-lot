using System.Collections.Generic;
using UnityEngine.UIElements;

namespace CoveredLot.Tests.Support
{
    /// <summary>
    /// What a screen shows, as the UI tests read it: an element is shown when its resolved display, and that of every
    /// element above it, is not None. The resolved style is the one the panel's last update worked out, so a test lets the
    /// panel update after a change before it reads what is shown. Each list is in the order of the element tree.
    /// </summary>
    public static class ShownElements
    {
        /// <summary>Whether the element and every element above it are displayed.</summary>
        public static bool IsShown(VisualElement element)
        {
            for (VisualElement? each = element; each is not null; each = each.parent)
            {
                if (each.resolvedStyle.display == DisplayStyle.None)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The shown elements of the type under the root, the root included.</summary>
        public static List<T> OfType<T>(VisualElement root)
            where T : VisualElement
        {
            var shown = new List<T>();
            foreach (T element in root.Query<T>().ToList())
            {
                if (IsShown(element))
                {
                    shown.Add(element);
                }
            }

            return shown;
        }

        /// <summary>The shown elements with the USS class under the root, the root included.</summary>
        public static List<VisualElement> WithClass(VisualElement root, string className)
        {
            var shown = new List<VisualElement>();
            foreach (VisualElement element in root.Query(className: className).ToList())
            {
                if (IsShown(element))
                {
                    shown.Add(element);
                }
            }

            return shown;
        }

        /// <summary>The texts of the shown labels under the root, the root included.</summary>
        public static List<string> LabelTexts(VisualElement root)
        {
            var texts = new List<string>();
            foreach (Label label in OfType<Label>(root))
            {
                texts.Add(label.text);
            }

            return texts;
        }

        /// <summary>The texts of the shown buttons under the root, the root included.</summary>
        public static List<string> ButtonTexts(VisualElement root)
        {
            var texts = new List<string>();
            foreach (Button button in OfType<Button>(root))
            {
                texts.Add(button.text);
            }

            return texts;
        }
    }
}
