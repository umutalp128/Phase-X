using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PhaseX.Player
{
    /// <summary>
    /// Drives the astronaut's Blender-authored Idle, Walk and Run clips through a runtime PlayableGraph (no Animator
    /// Controller asset). The three clips are mixed by how fast the player moves; the stride is locked to the same
    /// gait cycle that fires the footstep sounds, so each footfall is heard when a boot lands.
    /// </summary>
    public class AstronautAnimator : MonoBehaviour
    {
        public const string ModelPath = "Station/astronaut_engineer";

        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        readonly AnimationClipPlayable[] _clips = new AnimationClipPlayable[3];     // Idle, Walk, Run
        readonly float[] _length = new float[3];
        readonly float[] _weight = { 1f, 0f, 0f };
        float _idleTime;

        /// <summary>Returns null when the body has no rig or the clips are missing (the body then stays static).</summary>
        public static AstronautAnimator Attach(GameObject body)
        {
            if (body == null || !Application.isPlaying) return null;
            var animator = body.GetComponentInChildren<Animator>(true);
            var idle = PhaseXModelLibrary.Clip(ModelPath, "Idle");
            var walk = PhaseXModelLibrary.Clip(ModelPath, "Walk");
            var run = PhaseXModelLibrary.Clip(ModelPath, "Run");
            if (animator == null || idle == null || walk == null || run == null) return null;
            var a = animator.gameObject.AddComponent<AstronautAnimator>();
            a.Init(animator, idle, walk, run);
            return a;
        }

        void Init(Animator animator, params AnimationClip[] clips)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _graph = PlayableGraph.Create("PhaseXAstronaut");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, 3);
            for (int i = 0; i < 3; i++)
            {
                _length[i] = Mathf.Max(clips[i].length, 1e-4f);
                _clips[i] = AnimationClipPlayable.Create(_graph, clips[i]);
                _clips[i].SetApplyFootIK(false);
                _clips[i].SetSpeed(0);                     // every clip's time is set from the gait cycle below
                _graph.Connect(_clips[i], 0, _mixer, i);
                _mixer.SetInputWeight(i, _weight[i]);
            }
            AnimationPlayableOutput.Create(_graph, "PhaseXAstronaut", animator).SetSourcePlayable(_mixer);
            _graph.Play();
        }

        /// <summary>
        /// Called by the player every frame. <paramref name="gaitCycle"/> counts strides (two footsteps each);
        /// <paramref name="run01"/> is 0 at walking speed and 1 at full sprint.
        /// </summary>
        public void Tick(bool moving, float run01, float gaitCycle, float dt)
        {
            if (!_graph.IsValid()) return;
            float wIdle = moving ? 0f : 1f, wRun = moving ? Mathf.Clamp01(run01) : 0f, wWalk = moving ? 1f - wRun : 0f;
            float k = 1f - Mathf.Exp(-dt * 9f);
            _weight[0] = Mathf.Lerp(_weight[0], wIdle, k);
            _weight[1] = Mathf.Lerp(_weight[1], wWalk, k);
            _weight[2] = Mathf.Lerp(_weight[2], wRun, k);
            float sum = Mathf.Max(1e-4f, _weight[0] + _weight[1] + _weight[2]);
            for (int i = 0; i < 3; i++) _mixer.SetInputWeight(i, _weight[i] / sum);

            _idleTime = Mathf.Repeat(_idleTime + dt, _length[0]);
            _clips[0].SetTime(_idleTime);
            // The clips put the feet down at a quarter and three quarters of the stride; the footstep sounds fire at
            // whole and half strides. A quarter-stride offset lines the two up.
            float phase = Mathf.Repeat(gaitCycle + 0.25f, 1f);
            _clips[1].SetTime(phase * _length[1]);
            _clips[2].SetTime(phase * _length[2]);
        }

        void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
