using System.Collections;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class ScenarioInputFixtureTests : ScenarioInputFixture
    {
        [UnityTest]
        public IEnumerator VirtualInputPerformsAndCancelsAnActionAcrossFrames()
        {
            using var action = new InputAction(
                "Submit",
                InputActionType.Button,
                "<Keyboard>/enter"
            );
            var performed = 0;
            var cancelled = 0;
            action.performed += _ => performed++;
            action.canceled += _ => cancelled++;
            action.Enable();
            Assert.That(performed, Is.Zero);
            Press(Keyboard.enterKey);
            yield return null;
            Assert.That(performed, Is.EqualTo(1));
            Assert.That(action.IsPressed(), Is.True);
            Release(Keyboard.enterKey);
            yield return null;
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(action.IsPressed(), Is.False);
        }
    }
}
