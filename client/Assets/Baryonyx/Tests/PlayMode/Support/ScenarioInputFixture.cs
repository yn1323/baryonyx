using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Baryonyx.Tests.PlayMode
{
    // Derive real screen scenarios from this fixture. InputTestFixture restores
    // the original devices/settings, so tests do not consume physical input.
    public abstract class ScenarioInputFixture : InputTestFixture
    {
        protected Keyboard Keyboard { get; private set; }
        protected Mouse Mouse { get; private set; }

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            Keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse = InputSystem.AddDevice<Mouse>();
        }

        [TearDown]
        public override void TearDown()
        {
            Keyboard = null;
            Mouse = null;
            base.TearDown();
        }
    }
}
