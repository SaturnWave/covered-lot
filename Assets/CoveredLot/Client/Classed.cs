using System;
using System.Collections.Generic;
using CoveredLot.Core.Pictures;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// Makes the plain elements the screens are built of, each with its USS class (StyleNames), so the look stays in
    /// CoveredLot.uss and the code only names classes; and keeps a row of like elements at the length a picture asks for.
    /// </summary>
    internal static class Classed
    {
        /// <summary>An element with the class, which takes pointer events unless it is told otherwise.</summary>
        public static VisualElement Element(string className, PickingMode picking = PickingMode.Position)
        {
            var element = new VisualElement { pickingMode = picking };
            element.AddToClassList(className);
            return element;
        }

        /// <summary>An empty label with the class; the screens set its text from Copy or a picture.</summary>
        public static Label Label(string className, PickingMode picking = PickingMode.Position)
        {
            var label = new Label { pickingMode = picking };
            label.AddToClassList(className);
            return label;
        }

        /// <summary>A button of the screens (StyleNames.Button) with its text, from Copy, that runs the action when clicked.</summary>
        public static Button Button(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.AddToClassList(StyleNames.Button);
            return button;
        }

        /// <summary>
        /// Makes the list, and the container after the children it already holds, have exactly this many elements: new
        /// ones are made at the end, and the last ones are dropped.
        /// </summary>
        public static void Resize<T>(List<T> elements, VisualElement container, int count, Func<T> make)
            where T : VisualElement
        {
            while (elements.Count < count)
            {
                T made = make();
                elements.Add(made);
                container.Add(made);
            }

            while (elements.Count > count)
            {
                T last = elements[elements.Count - 1];
                elements.RemoveAt(elements.Count - 1);
                last.RemoveFromHierarchy();
            }
        }
    }
}
