using UnityEngine;
using UnityEngine.Scripting;

/// <summary>
/// Keeps engine classes in player builds that the game only uses indirectly.
/// GameObject.CreatePrimitive adds a collider to every primitive (a CapsuleCollider for cylinders and capsules, a
/// SphereCollider for spheres, a MeshCollider for quads and planes). With "Strip Engine Code" on (IL2CPP: WebGL,
/// consoles, mobile), a class that no script or scene references is removed from the build, and CreatePrimitive then
/// fails with "Can't add component because class 'CapsuleCollider' doesn't exist". Referencing the types here keeps
/// them. Assets/_PhaseX/link.xml does the same for the managed side.
/// </summary>
[Preserve]
public static class PhaseXBuildPreserve
{
    [Preserve]
    static readonly System.Type[] Kept =
    {
        typeof(BoxCollider), typeof(SphereCollider), typeof(CapsuleCollider), typeof(MeshCollider), typeof(CharacterController),
        typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer), typeof(TrailRenderer),
        typeof(ParticleSystem), typeof(ParticleSystemRenderer), typeof(TextMesh), typeof(Light), typeof(AudioSource), typeof(AudioListener),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Touch()
    {
        // reading the array keeps the field (and with it the type references) from being optimised away
        if (Kept.Length == 0) Debug.LogWarning("[PhaseX] Build preserve list is empty.");
    }
}
