using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Plays one Blender-authored animation clip of a model from PhaseXModelLibrary through a runtime PlayableGraph
/// (no Animator Controller asset). Two ways to use it:
/// loop / one-shot playback that advances by itself (engine plume flicker), or "manual" mode where the owner sets
/// the time every frame (stage separation follows simulation time, the launch lever follows the lever angle).
/// </summary>
public class PhaseXClipPlayer : MonoBehaviour
{
    PlayableGraph _graph;
    AnimationClipPlayable _playable;
    float _time;

    public float Length { get; private set; }
    public bool Loop { get; set; }
    /// <summary>When true the clip only moves through SetTime / SetNormalized.</summary>
    public bool Manual { get; set; }
    public float Speed { get; set; } = 1f;

    /// <summary>
    /// Start a clip on a model returned by PhaseXModelLibrary.InstantiateModel. Returns null (and changes nothing)
    /// when the model or the clip does not exist, so callers can keep a code-driven fallback.
    /// </summary>
    public static PhaseXClipPlayer Attach(GameObject model, string modelPath, string clipName, bool loop, bool manual = false, float speed = 1f)
    {
        if (model == null || !Application.isPlaying) return null;
        var clip = PhaseXModelLibrary.Clip(modelPath, clipName);
        var animator = model.GetComponentInChildren<Animator>(true);
        if (clip == null || animator == null) return null;
        var player = animator.gameObject.AddComponent<PhaseXClipPlayer>();
        player.Init(animator, clip, loop, manual, speed);
        return player;
    }

    void Init(Animator animator, AnimationClip clip, bool loop, bool manual, float speed)
    {
        Loop = loop; Manual = manual; Speed = speed;
        Length = Mathf.Max(clip.length, 1e-4f);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _graph = PlayableGraph.Create("PhaseXClip_" + clip.name);
        _graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);    // the time is set here, never by the graph
        _playable = AnimationClipPlayable.Create(_graph, clip);
        _playable.SetApplyFootIK(false);
        _playable.SetSpeed(0);
        AnimationPlayableOutput.Create(_graph, "PhaseXClip", animator).SetSourcePlayable(_playable);
        _graph.Play();
        SetTime(0f);
    }

    public void SetTime(float seconds)
    {
        _time = Loop ? Mathf.Repeat(seconds, Length) : Mathf.Clamp(seconds, 0f, Length);
        if (_playable.IsValid()) _playable.SetTime(_time);
    }

    /// <summary>0 = first frame, 1 = last frame.</summary>
    public void SetNormalized(float u) => SetTime(Mathf.Clamp01(u) * Length);

    void Update()
    {
        if (!Manual) SetTime(_time + Time.deltaTime * Speed);
    }

    void OnDestroy()
    {
        if (_graph.IsValid()) _graph.Destroy();
    }
}
