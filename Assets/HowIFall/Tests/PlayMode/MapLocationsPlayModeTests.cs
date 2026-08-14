using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HowIFall.PlayModeTests
{
    [Category("MapLocations")]
    public sealed class MapLocationsPlayModeTests
    {
        [UnityTest]
        public IEnumerator TechnicalMap_TransitionsOnlyThroughAvailableLocation_AndRestoresNormalFlow()
        {
            SaveManager.ScreenshotCaptureOverrideForTests=CreatePreviewTexture;
            try
            {
                yield return LoadScene("VNPrototype");
                yield return WaitFor(()=>VNDialogueController.Instance!=null&&VNDialogueController.Instance.IsRuntimeReady,"VN runtime did not become ready.");
                var dialogue=VNDialogueController.Instance; var map=Resources.Load<MapSceneData>("MapLocations/TechnicalMap"); Assert.That(map,Is.Not.Null);
                if(EventSystem.current==null)new GameObject("MapLocationsEventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
                Assert.That(dialogue.TryStartMapScene(map,out string failure),Is.True,failure);
                var runtime=dialogue.ActiveMapSceneController; Assert.That(runtime.IsRunning,Is.True); Assert.That(dialogue.CanAdvanceDialogue,Is.False); Assert.That(runtime.IsLocationAvailable("test_dorm"),Is.True); Assert.That(runtime.IsLocationAvailable("test_cafe"),Is.False);
                Assert.That(runtime.TryActivateLocation("test_cafe"),Is.False); Assert.That(runtime.IsRunning,Is.True);
                Assert.That(dialogue.OpenGameMenu(),Is.True); Assert.That(dialogue.CanSave,Is.False); Assert.That(dialogue.CanLoad,Is.False); Assert.That(dialogue.GameMenuController.View.GetButton(VNGameMenuAction.Save).interactable,Is.False); Assert.That(dialogue.GameMenuController.Close(),Is.True); Assert.That(runtime.IsRunning,Is.True);
                foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(3840,2160),new Vector2Int(1024,768)}){Screen.SetResolution(resolution.x,resolution.y,false);yield return null;Canvas.ForceUpdateCanvases();AssertInside(runtime,"test_dorm");AssertInside(runtime,"test_university");AssertInside(runtime,"test_cafe");}
                Click(runtime.GetLocationButton("test_dorm")); yield return null;
                Assert.That(runtime.IsRunning,Is.False); Assert.That(dialogue.CanAdvanceDialogue,Is.True); Assert.That(GameState.Instance.currentSceneId,Is.EqualTo("map_test_dorm")); Assert.That(dialogue.CanSave,Is.True); Assert.That(dialogue.CanLoad,Is.True); Assert.That(dialogue.HasActiveSpecialMode,Is.False);
            }
            finally { SaveManager.ScreenshotCaptureOverrideForTests=null; }
        }
        private static Texture2D CreatePreviewTexture(){var texture=new Texture2D(2,2,TextureFormat.RGB24,false);texture.SetPixels(new[]{Color.black,Color.black,Color.black,Color.black});texture.Apply();return texture;}
        private static void Click(Button button){Assert.That(button,Is.Not.Null.And.Property("interactable").True);ExecuteEvents.Execute<IPointerClickHandler>(button.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);}
        private static void AssertInside(MapSceneController runtime,string id){var image=runtime.DisplayedImageRect;var button=runtime.GetLocationButton(id).GetComponent<RectTransform>();var a=new Vector3[4];var b=new Vector3[4];image.GetWorldCorners(a);button.GetWorldCorners(b);foreach(var corner in b){Assert.That(corner.x,Is.InRange(a[0].x-.1f,a[2].x+.1f));Assert.That(corner.y,Is.InRange(a[0].y-.1f,a[2].y+.1f));}}
        private static IEnumerator LoadScene(string name){var op=SceneManager.LoadSceneAsync(name,LoadSceneMode.Single);while(!op.isDone)yield return null;yield return null;}
        private static IEnumerator WaitFor(System.Func<bool> predicate,string error){float began=Time.realtimeSinceStartup;while(!predicate()){Assert.That(Time.realtimeSinceStartup-began,Is.LessThan(15f),error);yield return null;}}
    }
}
