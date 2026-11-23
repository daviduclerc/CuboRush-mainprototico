using UnityEditor;
using UnityEngine;

/// <summary>
/// Só no Editor: esconde os ícones de lâmpada/sol das luzes (gizmos), que apareciam
/// como "flashes" brancos e vermelhos no Game view. Não muda nada no jogo final.
/// Para mostrar de novo: menu Cubo Rush > Mostrar ícones das luzes.
/// </summary>
[InitializeOnLoad]
static class EsconderIconesDeLuz
{
    const string Chave = "CuboRush.MostrarIconesDeLuz";

    static EsconderIconesDeLuz()
    {
        EditorApplication.delayCall += Aplicar;
    }

    static void Aplicar()
    {
        bool mostrar = EditorPrefs.GetBool(Chave, false);
        GizmoUtility.SetIconEnabled(typeof(Light), mostrar);
    }

    [MenuItem("Cubo Rush/Mostrar ícones das luzes")]
    static void Alternar()
    {
        EditorPrefs.SetBool(Chave, !EditorPrefs.GetBool(Chave, false));
        Aplicar();
    }

    [MenuItem("Cubo Rush/Mostrar ícones das luzes", true)]
    static bool AlternarValidar()
    {
        Menu.SetChecked("Cubo Rush/Mostrar ícones das luzes", EditorPrefs.GetBool(Chave, false));
        return true;
    }
}
