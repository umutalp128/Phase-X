namespace PhaseX.Player
{
    /// <summary>Anything in the 3D station the player can look at and use with [E].</summary>
    public interface IInteractable
    {
        /// <summary>Prompt shown under the crosshair, e.g. "[E] Use Spacecraft CAD Terminal".</summary>
        string GetInteractionPrompt();

        void Interact(PhaseXPlayerController player);
    }
}
