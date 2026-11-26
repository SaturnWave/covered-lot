using CoveredLot.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The game's one MonoBehaviour and its composition root (design section 8.5). It sits on the Main scene's Game
    /// object next to the UIDocument and finds that document itself, so it has no serialized reference fields. For now
    /// it shows only the game's title; later pieces build the screens here.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        /// <summary>The USS class of the title label; CoveredLot.uss gives it its size.</summary>
        private const string TitleClass = "covered-lot-title";

        /// <summary>
        /// UIDocument runs at execution order -100 and creates its root element when it is enabled, so the root exists by
        /// the time this Awake runs. Without panel settings that root is never attached to a panel, so nothing shows.
        /// </summary>
        private void Awake()
        {
            UIDocument? document = GetComponent<UIDocument>();
            if (document == null)
            {
                Debug.LogError(nameof(GameRoot) + " needs a " + nameof(UIDocument) + " on the same GameObject, so it disables itself.", this);
                enabled = false;
                return;
            }

            var title = new Label(Copy.GameTitle);
            title.AddToClassList(TitleClass);
            document.rootVisualElement.Add(title);
        }
    }
}
