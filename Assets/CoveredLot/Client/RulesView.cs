using System;
using CoveredLot.Core;
using CoveredLot.Core.Pictures;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The rules (piece 13, on the user's word of 2026-11-28): the heading "How to play", a line for each rule, and a Back
    /// button. The screen host shows them in place of the screen whose Rules button opened them, and Back brings that
    /// screen back. Opening and closing the rules changes no screen of the navigator and no session: a level left for the
    /// rules is just as it was.
    /// </summary>
    public sealed class RulesView : VisualElement
    {
        /// <param name="back">What the Back button does: close the rules.</param>
        public RulesView(Action back)
        {
            AddToClassList(StyleNames.Rules);
            Label heading = Classed.Label(StyleNames.Title);
            heading.text = Copy.RulesHeading;
            Add(heading);
            foreach (string rule in Copy.RulesLines)
            {
                Label line = Classed.Label(StyleNames.RulesLine);
                line.text = rule;
                Add(line);
            }

            Add(Classed.Button(Copy.Back, back));
        }
    }
}
