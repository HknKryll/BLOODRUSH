using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace Bloodrush.UI.Menu
{
// EventSystem'i klavye + gamepad calisacak sekilde kurar.
//
// NEDEN YENI INPUT SYSTEM: projede com.unity.inputsystem 1.14.0 kurulu ve
// activeInputHandler = Both. Yani menude InputSystemUIInputModule kullanmak oynanisin
// legacy Input kodunu HIC etkilemiyor; karsiliginda gamepad sol cubuk/d-pad, A/B ve
// klavye ok/Enter/Esc navigasyonu kendiliginden geliyor. Eski StandaloneInputModule ile
// gamepad icin elle eksen tanimlamak gerekirdi.
//
// EventSystem KODLA kurulur — hicbir sahneye dokunmak gerekmez.
public static class MenuNavigation
{
    public static EventSystem Ensure()
    {
        var es = Object.FindFirstObjectByType<EventSystem>();

        if (es == null)
        {
            var go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[MenuNavigation] EventSystem kuruldu (InputSystemUIInputModule).");
            return es;
        }

        // Sahnede eski modul varsa yenisiyle degistir — yoksa gamepad calismaz.
        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) Object.Destroy(legacy);
            es.gameObject.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[MenuNavigation] EventSystem yeni Input System modulune gecirildi.");
        }
        return es;
    }

    // Bir menu acilinca ilk ogeyi sec — gamepad/klavye kullanicisi bosluga bakmasin.
    public static void Focus(GameObject first)
    {
        var es = EventSystem.current;
        if (es == null || first == null) return;
        es.SetSelectedGameObject(null);
        es.SetSelectedGameObject(first);
    }

    // Dikey listeyi birbirine baglar: en alttan asagi basinca basa doner (sarmal).
    public static void LinkVertical(params Selectable[] items)
    {
        if (items == null || items.Length == 0) return;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp   = items[(i - 1 + items.Length) % items.Length];
            nav.selectOnDown = items[(i + 1) % items.Length];
            items[i].navigation = nav;
        }
    }

    // Sekme listesi (sol) ile icerik (sag) arasinda yatay gecis kurar.
    public static void LinkHorizontal(Selectable left, Selectable right)
    {
        if (left != null)
        {
            var n = left.navigation; n.mode = Navigation.Mode.Explicit;
            n.selectOnRight = right; left.navigation = n;
        }
        if (right != null)
        {
            var n = right.navigation; n.mode = Navigation.Mode.Explicit;
            n.selectOnLeft = left; right.navigation = n;
        }
    }
}
}
