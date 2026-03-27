using UnityEngine;

/// <summary>
/// Contract for gameplay objects that can be interacted with by a player interactor.
/// </summary>
public interface IInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract { get; }
    void Interact(GameObject interactor);
}
