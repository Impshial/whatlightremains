using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class PauseMenuPlayModeTests
    {
        [UnityTest]
        public IEnumerator PauseAndResume_OwnTimeAndCursorOnlyWhileMenuIsVisible()
        {
            yield return SceneManager.LoadSceneAsync("Foundation", LoadSceneMode.Single);
            PauseMenuController pause = Object.FindObjectsByType<PauseMenuController>(FindObjectsInactive.Include).Single();
            PlayerLook look = Object.FindAnyObjectByType<PlayerLook>();
            look.CaptureCursor();

            pause.Pause();
            Assert.That(PauseMenuController.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(pause.MenuRoot.activeSelf, Is.True);
            Assert.That(look.IsCursorCaptured, Is.False);

            pause.Resume();
            yield return null;
            Assert.That(PauseMenuController.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(pause.MenuRoot.activeSelf, Is.False);
            Assert.That(look.IsCursorCaptured, Is.True);
        }

        [UnityTest]
        public IEnumerator EscapeRequest_DefersToCreateAndDeleteModes()
        {
            yield return SceneManager.LoadSceneAsync("Foundation", LoadSceneMode.Single);
            PauseMenuController pause = Object.FindObjectsByType<PauseMenuController>(FindObjectsInactive.Include).Single();
            PlayerLook look = Object.FindAnyObjectByType<PlayerLook>();
            RoomCreationController creation = Object.FindAnyObjectByType<RoomCreationController>();
            RoomDeletionController deletion = Object.FindAnyObjectByType<RoomDeletionController>();
            look.CaptureCursor();

            Assert.That(creation.EnterCreateMode(), Is.True);
            Assert.That(pause.HandleEscapeRequest(), Is.False);
            Assert.That(PauseMenuController.IsPaused, Is.False);
            creation.CancelCreateMode();

            Assert.That(deletion.EnterDeleteMode(), Is.True);
            Assert.That(pause.HandleEscapeRequest(), Is.False);
            Assert.That(PauseMenuController.IsPaused, Is.False);
            deletion.CancelDeleteMode();

            Assert.That(pause.HandleEscapeRequest(), Is.True);
            Assert.That(PauseMenuController.IsPaused, Is.True);
            pause.Resume();
        }
    }
}
