using UnityEngine.Events;
using UnityEngine.UI;

public static class Utils {
        
    /// <summary>
    /// Binds multiple actions to button
    /// </summary>
    /// <param name="button"></param>
    /// <param name="unityAction"></param>
    public static void BindButtonPlusParamsAction(Button button, params UnityAction[] unityAction) {
        foreach (var action in unityAction) {
            button.onClick.AddListener(action);
        }
    }
}