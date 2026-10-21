namespace Baryonyx.Adventure
{
    /// <summary>
    /// The exploration moment lab's way into the exploration screen (<see cref="ExplorationMoments"/>):
    /// a chest set down in the room the party stands in, closed or open, whatever the room is,
    /// so a chest can be opened again and again on the sample route.
    /// </summary>
    public sealed partial class ExplorationView
    {
        /// <summary>Puts the chest in the party's room, shown at once, closed or open.</summary>
        public void ShowChestHere(bool open)
        {
            if (ChestGroup == null || ShownRun == null)
                return;
            chestRoom = ShownRun.RoomId;
            chestAlpha = chestTarget = 1f;
            ChestGroup.alpha = 1f;
            ChestGroup.blocksRaycasts = false;
            ChestGroup.interactable = false;
            if (ChestButton != null)
                ChestButton.gameObject.SetActive(false);
            SetChestOpen(open);
            PlaceChestOnMap();
        }

        /// <summary>Opens or closes the chest shown, without moving it.</summary>
        public void SetChestOpen(bool open)
        {
            if (ChestSprite != null)
                ChestSprite.texture = open ? ChestOpen : ChestClosed;
        }

        /// <summary>Takes the chest away at once.</summary>
        public void HideChest()
        {
            if (ChestGroup == null)
                return;
            chestAlpha = chestTarget = 0f;
            ChestGroup.alpha = 0f;
        }
    }
}
