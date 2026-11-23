#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Só no Editor: aperte F1 durante o Play para mostrar a imagem de referência
/// por cima do jogo (semitransparente) e comparar o enquadramento e as cores.
/// F2 alterna a opacidade entre 40% e 100%.
/// A imagem fica em Resources/Referencia/ReferenciaVisual.png.
/// </summary>
public class ReferenciaVisual : MonoBehaviour
{
    static readonly float[] Opacidades = { 0.4f, 1f };

    RawImage imagem;
    int indiceOpacidade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (FindAnyObjectByType<ReferenciaVisual>() != null) return;
        GameObject obj = new GameObject("ReferenciaVisual (F1)");
        DontDestroyOnLoad(obj);
        obj.AddComponent<ReferenciaVisual>();
    }

    void Start()
    {
        Texture2D textura = Resources.Load<Texture2D>("Referencia/ReferenciaVisual");
        if (textura == null) { enabled = false; return; }

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // acima de todo o HUD

        GameObject objImagem = new GameObject("Imagem");
        objImagem.transform.SetParent(transform, false);
        imagem = objImagem.AddComponent<RawImage>();
        imagem.texture = textura;
        imagem.raycastTarget = false;

        RectTransform rt = imagem.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Mantém a proporção da imagem e cobre a tela inteira (corta o que sobrar).
        AspectRatioFitter ajuste = objImagem.AddComponent<AspectRatioFitter>();
        ajuste.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        ajuste.aspectRatio = (float)textura.width / textura.height;

        AplicarOpacidade();
        objImagem.SetActive(false);
    }

    void Update()
    {
        if (imagem == null) return;
        Keyboard teclado = Keyboard.current;
        if (teclado == null) return;

        if (teclado.f1Key.wasPressedThisFrame)
            imagem.gameObject.SetActive(!imagem.gameObject.activeSelf);

        if (teclado.f2Key.wasPressedThisFrame)
        {
            indiceOpacidade = (indiceOpacidade + 1) % Opacidades.Length;
            AplicarOpacidade();
        }
    }

    void AplicarOpacidade()
    {
        imagem.color = new Color(1f, 1f, 1f, Opacidades[indiceOpacidade]);
    }
}
#endif
