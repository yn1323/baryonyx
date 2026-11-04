using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;

namespace Baryonyx.Library
{
    /// <summary>
    /// Entering a dungeon from where a thing is found (doc/features/library.md の入手先): the
    /// shared dialog asks with the dungeon's name as its title, "やめる" closes it and "行く" sets
    /// out on the adventure there through the guide screen (<see cref="GuideMenuView.SetOut"/>),
    /// as the travel office's departure does. The library and the workshop's craft use it.
    /// </summary>
    public static class DungeonEntry
    {
        public const string Question = "このダンジョンに入ります。行きますか？";
        public const string StayChoice = "やめる";
        public const string EnterChoice = "行く";
        public const string FailedMessage = "このダンジョンには入れません";

        public static void Ask(GameDialog dialog, GuideMenuView guide, LibraryDropPlaces.Place place)
        {
            if (dialog == null)
                return;
            dialog.Show(
                place.Name,
                Question,
                null,
                dialog.Hide,
                DialogStyle.Row,
                new DialogChoice(StayChoice, dialog.Hide),
                new DialogChoice(
                    EnterChoice,
                    () => Enter(dialog, guide, place.Destination),
                    role: ButtonRole.Primary
                )
            );
        }

        private static void Enter(GameDialog dialog, GuideMenuView guide, string destination)
        {
            dialog.Hide();
            if (guide == null)
                return;
            // 始める間とシーンを移る間は旅の案内所と同じ出発が受け持ち、断られたら通知の帯で知らせる。
            if (guide.SetOut == null || !guide.SetOut(destination))
                guide.ShowToast(FailedMessage);
        }
    }
}
