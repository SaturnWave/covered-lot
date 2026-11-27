using CoveredLot.Core;
using CoveredLot.Core.Pictures;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The problem screen until piece 13 replaces it with the real one: the one sentence players see when the catalogue
    /// did not load, Copy.LevelsNotLoaded. Piece 13 adds the development lines, one for each problem.
    /// </summary>
    public sealed class InterimProblemView : VisualElement
    {
        public InterimProblemView()
        {
            AddToClassList(StyleNames.Problems);
            Label message = Classed.Label(StyleNames.Message);
            message.text = Copy.LevelsNotLoaded;
            Add(message);
        }
    }
}
