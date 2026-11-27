using System.Collections.Generic;
using CoveredLot.Core;
using CoveredLot.Core.Navigation;
using CoveredLot.Core.Pictures;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The problem screen (design sections 7 and 9), which a catalogue that did not load shows and no action leaves: the
    /// one sentence players see, Copy.LevelsNotLoaded, and in development builds a line for each of the catalogue's
    /// problems, in its order, as Copy.ProblemLine writes it.
    /// </summary>
    public sealed class ProblemView : VisualElement
    {
        private readonly bool _developmentBuild;
        private readonly List<Label> _lines = new List<Label>();

        /// <param name="developmentBuild">Whether the build is a development build, the only kind that shows the problems' lines.</param>
        public ProblemView(bool developmentBuild)
        {
            _developmentBuild = developmentBuild;
            AddToClassList(StyleNames.Problems);
            Label message = Classed.Label(StyleNames.Message);
            message.text = Copy.LevelsNotLoaded;
            Add(message);
        }

        /// <summary>Shows a line for each problem in development builds; a release build shows the sentence alone.</summary>
        public void Show(AppScreen.Problems problems)
        {
            int shown = _developmentBuild ? problems.List.Count : 0;
            while (_lines.Count < shown)
            {
                Label line = Classed.Label(StyleNames.ProblemLine);
                _lines.Add(line);
                Add(line);
            }

            while (_lines.Count > shown)
            {
                _lines[_lines.Count - 1].RemoveFromHierarchy();
                _lines.RemoveAt(_lines.Count - 1);
            }

            for (int index = 0; index < shown; index++)
            {
                _lines[index].text = Copy.ProblemLine(problems.List[index]);
            }
        }
    }
}
