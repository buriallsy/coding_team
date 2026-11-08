using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowingSun.Editor
{
    public static partial class SkywellChecks
    {
        public static void RunObservation()=>StartCheck("SKYWELL_OBSERVATION",ObservationSequence());

        private static IEnumerator ObservationSequence()
        {
            report.Add("EXPLICIT OBSERVATION FIXTURES: player is placed on the west entrance, on the village bridge approach and beside the village weather station. The short bridge-to-marker path uses actual movement. Real keyboard E/Escape/R, movement and weather input are used; no progress or success state is injected. This is not a continuous route or first-player comprehension test. Mouse and multi-resolution visual review are separate manual checks.");
            court.ResetCourt();yield return Wait(.4f);
            var observation=court.Observation;
            Require(observation&&observation.Available&&observation.Image&&observation.Image.width==1672&&observation.Image.height==941,
                "Authored observation point has the supplied 1672×941 landscape image without power-of-two resizing.");
            Require(!string.IsNullOrWhiteSpace(observation.Title)&&!string.IsNullOrWhiteSpace(observation.Caption),
                "Observation has authored title and environmental clue text.");
            Require(!observation.IsOpen,"Reset begins with the landscape closed.");

            court.Player.Teleport(court.Origin+new Vector3(-30,.03f,0));yield return Wait(.3f);
            Require(!court.Prompt().Contains("E 观测"),"The distant west entrance does not advertise the village observation interaction.");
            yield return Press(Key.E);
            Require(!observation.IsOpen,"E away from the observation point cannot open the landscape.");

            court.ResetCourt();yield return Wait(.3f);
            Vector3 nearby=observation.transform.position+new Vector3(0,.03f,-.8f);
            court.Player.Teleport(new Vector3(46,26.03f,108));yield return Wait(.4f);
            TraceObservationInput("bridge approach fixture");
            yield return Walk(new Vector3(48,26.03f,108),"Village bridge approach to observation-side landing");
            yield return Walk(nearby,"Walk from the bridge landing to the observation marker");
            Require(court.Prompt().Contains("山阴村")&&court.Prompt().Contains("E 观测"),
                "The nearby marker is selected through the production interaction resolver.");
            Capture(Path.Combine(Folder,"observation-nearby.png"));yield return Wait(.2f);

            // A non-default shoulder heading makes a modal restore regression observable.
            yield return EnsureCameraMode(true);yield return CameraMousePulse(new Vector2(65,-20));
            Quaternion cameraRotation=court.CameraRig.ViewCamera.transform.rotation;
            bool shoulder=court.CameraRig.IsShoulderView;
            var selected=court.SelectedWeather;
            var tokens=court.Consoles.Select(s=>s.Token).ToArray();
            float water=court.WaterLevel,height=court.Height,yaw=court.Yaw;
            input=new KeyboardState(Key.E);yield return Wait(.12f);
            Require(!observation.IsOpen&&!court.WeatherTargeting,"A short E press waits for release without opening observation or weather targeting.");
            input=new KeyboardState();yield return Wait(.2f);
            Require(observation.IsOpen&&court.IsObserving&&!court.WeatherTargeting&&!court.WeatherSelectionHeld&&court.CameraRig.InputSuppressed,
                "Releasing short E opens observation and blocks camera input without casting weather.");
            Capture(Path.Combine(Folder,"observation-open.png"));yield return Wait(.2f);

            Vector3 heldPosition=court.Player.FeetPosition;
            input=new KeyboardState(Key.W,Key.D,Key.Space);yield return Wait(.65f);input=new KeyboardState();yield return Wait(.15f);
            Require(Vector3.ProjectOnPlane(court.Player.FeetPosition-heldPosition,Vector3.up).magnitude<.02f&&!court.Player.IsDropping,
                "WASD and Space cannot walk or step off the platform beneath the observation image.");
            input=new KeyboardState(Key.Tab,Key.Digit2,Key.Q,Key.V,Key.F,Key.Z);yield return Wait(.6f);
            input=new KeyboardState();yield return Wait(.2f);
            Require(observation.IsOpen&&!court.WeatherSelectionHeld&&!court.WeatherTargeting&&court.SelectedWeather==selected,
                "Tab, number selection and Q cannot open or alter hidden weather controls while observing.");
            Require(court.CameraRig.IsShoulderView==shoulder&&Quaternion.Angle(cameraRotation,court.CameraRig.ViewCamera.transform.rotation)<.03f,
                "Held Z and removed legacy camera keys do not change the underlying mode or actual camera rotation while observing.");
            Require(tokens.SequenceEqual(court.Consoles.Select(s=>s.Token))&&Mathf.Abs(water-court.WaterLevel)<.001f
                &&Mathf.Abs(height-court.Height)<.001f&&Mathf.Abs(yaw-court.Yaw)<.001f,
                "Observation input does not insert/remove tokens or drive the tower mechanisms.");

            yield return Press(Key.E);yield return Wait(.2f);
            Require(!observation.IsOpen&&!court.IsObserving&&!court.CameraRig.InputSuppressed&&!court.WeatherTargeting,
                "A second E closes the landscape and releases player/camera input.");
            Require(tokens.SequenceEqual(court.Consoles.Select(s=>s.Token))&&court.SelectedWeather==selected,
                "The closing E is consumed without a weather action.");
            Vector3 beforeMove=court.Player.FeetPosition;
            yield return MoveInDirection(Vector3.back,.35f);
            Require(Vector3.ProjectOnPlane(court.Player.FeetPosition-beforeMove,Vector3.up).magnitude>.15f,
                "Real movement resumes after closing the landscape.");

            court.Player.Teleport(nearby);yield return Wait(.3f);yield return Press(Key.E);
            Require(observation.IsOpen,"The same observation point can be reopened after leaving its image.");
            yield return Press(Key.Escape);
            Require(!observation.IsOpen&&!court.IsObserving&&!court.CameraRig.InputSuppressed&&!court.WeatherTargeting,
                "Escape closes the landscape without leaving a movement or camera lock.");
            yield return EnsureCameraMode(false);
            Require(!court.CameraRig.IsShoulderView,"Actual Z input selects fixed view before the close-frame regression check.");
            Quaternion fixedRotation=court.CameraRig.ViewCamera.transform.rotation;
            yield return Press(Key.E);
            Require(observation.IsOpen&&!court.CameraRig.IsShoulderView,"E can open the landscape while preserving the underlying fixed view.");
            input=new KeyboardState(Key.Escape,Key.Z);yield return Wait(.15f);
            input=new KeyboardState();yield return Wait(.15f);
            Require(!observation.IsOpen&&!court.CameraRig.IsShoulderView&&Quaternion.Angle(fixedRotation,court.CameraRig.ViewCamera.transform.rotation)<.03f,
                "Escape plus Z on the closing frame returns to the same fixed view without toggling mode or resetting heading.");
            Require(!court.CameraRig.InputSuppressed&&!court.WeatherTargeting,"Close-frame camera suppression releases after Escape without a stuck input gate.");
            yield return Press(Key.E);Require(observation.IsOpen,"Observation remains usable after Escape.");
            yield return Press(Key.R);yield return Wait(.4f);
            Require(!observation.IsOpen&&!court.IsObserving&&!court.CameraRig.InputSuppressed&&!court.WeatherTargeting,
                "R during observation closes the image and releases all observation input gates.");
            CheckReset();

            // A real held-E pointer cast must not fall through to short-E observation.
            yield return WeatherFixturePlace(3);
            yield return SetWeather(SkywellToken.Rain,0);
            Require(!observation.IsOpen&&court.Consoles[3].Token==SkywellToken.Rain&&court.Consoles[3].Zone.IsRain,
                "Holding E and clicking the village target produces local Rain without opening observation.");
            yield return WeatherFixtureLeave(3);
            Require(!observation.IsOpen&&!court.WeatherTargeting&&!court.CameraRig.InputSuppressed,
                "Leaving the village zone returns Rain without reopening observation or camera locks.");
            yield return Press(Key.R);yield return Wait(.3f);CheckReset();
        }

        private static void TraceObservationInput(string label)
        {
            var current=Keyboard.current;
            report.Add("OBS INPUT "+label+" frame="+Time.frameCount+" t="+Time.time.ToString("F3")
                +" open="+court.Observation.IsOpen+" playerEnabled="+court.Player.enabled
                +" autoPositioning="+court.Player.IsAutoPositioning+" journey="+court.Journey.Travel
                +" targeting="+court.WeatherTargeting+" focused="+Application.isFocused
                +" keyboard="+(current==null?"none":current.deviceId.ToString())
                +" expectedKeyboard="+(keyboard==null?"none":keyboard.deviceId.ToString())
                +" ePressedThisFrame="+(current!=null&&current.eKey.wasPressedThisFrame)
                +" eHeld="+(current!=null&&current.eKey.isPressed)
                +" prompt="+court.Prompt());
            SaveProgress();
        }
    }
}
