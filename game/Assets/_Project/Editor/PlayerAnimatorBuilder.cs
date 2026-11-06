using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using SevenDays.Player;

namespace SevenDays.EditorTools
{
    /// <summary>
    /// Menú 7Days > Crear Animator del jugador: genera Assets/_Project/Animation/PlayerAnimator.controller con todos los parámetros de PlayerAnimatorHooks,
    /// un blend tree 2D de 8 direcciones (MoveX/MoveY) con las ranuras vacías para las animaciones, estados de salto/caída, vault y cobertura, y una capa
    /// "UpperBody" para disparo, recarga y cuerpo a cuerpo. Solo falta arrastrar los clips de Quaternius a cada ranura (docs/ANIMATION_HOOKS.md).
    /// </summary>
    public static class PlayerAnimatorBuilder
    {
        private const string Dir = "Assets/_Project/Animation";
        private const string Path = Dir + "/PlayerAnimator.controller";

        [MenuItem("7Days/Crear Animator del jugador")]
        public static void Build()
        {
            if (File.Exists(Path) && !EditorUtility.DisplayDialog("PlayerAnimator", "Ya existe " + Path + ". ¿Sobrescribirlo (se pierden los clips asignados)?", "Sobrescribir", "Cancelar")) return;
            Directory.CreateDirectory(Dir);
            var c = AnimatorController.CreateAnimatorControllerAtPath(Path);
            foreach (var (name, type) in PlayerAnimatorHooks.Parameters) c.AddParameter(name, type);

            // --- capa base: locomoción
            var sm = c.layers[0].stateMachine;
            var loco = c.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX"; tree.blendParameterY = "MoveY";
            // idle, 4 direcciones caminando (0.6) y corriendo hacia delante (1) y 4 diagonales: las ranuras quedan vacías hasta tener clips
            foreach (var p in new[] { new Vector2(0, 0), new Vector2(0, 0.6f), new Vector2(0, -0.6f), new Vector2(-0.6f, 0), new Vector2(0.6f, 0),
                                       new Vector2(-0.45f, 0.45f), new Vector2(0.45f, 0.45f), new Vector2(-0.45f, -0.45f), new Vector2(0.45f, -0.45f), new Vector2(0, 1f) })
                tree.AddChild(null, p);
            sm.defaultState = loco;

            var crouch = sm.AddState("Crouch");
            var cover = sm.AddState("Cover");
            var air = sm.AddState("Airborne");
            var vault = sm.AddState("Vault");
            Link(loco, crouch, "IsCrouching", true); Link(crouch, loco, "IsCrouching", false);
            Link(loco, cover, "IsInCover", true); Link(cover, loco, "IsInCover", false);
            Link(loco, air, "IsGrounded", false); Link(air, loco, "IsGrounded", true);
            var any = sm.AddAnyStateTransition(vault); any.AddCondition(AnimatorConditionMode.If, 0, "IsVaulting"); any.duration = 0.05f;
            Link(vault, loco, "IsVaulting", false);

            // --- capa superior: disparo, recarga y cuerpo a cuerpo (máscara de torso cuando haya avatar)
            c.AddLayer("UpperBody");
            var layers = c.layers; layers[1].defaultWeight = 1f; c.layers = layers;
            var up = c.layers[1].stateMachine;
            var idle = up.AddState("Empty"); up.defaultState = idle;
            foreach (var (state, trig) in new[] { ("Fire", "Fire"), ("Reload", "Reload"), ("Melee", "MeleeSwing"), ("MeleeHeavy", "MeleeHeavy") })
            {
                var s = up.AddState(state);
                var t = up.AddAnyStateTransition(s); t.AddCondition(AnimatorConditionMode.If, 0, trig); t.duration = 0.05f;
                var back = s.AddTransition(idle); back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.1f;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[7Days] Creado " + Path + " con " + PlayerAnimatorHooks.Parameters.Length + " parámetros. Asigna los clips siguiendo docs/ANIMATION_HOOKS.md.");
        }

        private static void Link(AnimatorState from, AnimatorState to, string param, bool value)
        {
            var t = from.AddTransition(to);
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
            t.hasExitTime = false; t.duration = 0.12f;
        }
    }
}
