using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun
{
    public sealed partial class SkywellController
    {
        [Header("天气选择与交互")]
        [Range(.15f,.8f),InspectorName("长按展开时间（秒）")] public float weatherHoldTime=.32f;
        [Min(5), InspectorName("可见目标施令距离（米）")] public float weatherCastRange=45;
        [Min(1), InspectorName("远程施令离位回牌距离（米）")] public float remoteWeatherMoveRadius=3.5f;
        [SerializeField] private SkywellWeatherTarget[] weatherTargets=Array.Empty<SkywellWeatherTarget>();
        private Vector3[] remoteWeatherOrigins=Array.Empty<Vector3>();
        private bool[] remoteWeatherActive=Array.Empty<bool>();
        public SkywellWeatherTarget[] WeatherTargets=>weatherTargets;
        public SkywellToken SelectedWeather {get;private set;}=SkywellToken.Rain;
        public SkywellToken WeatherCandidate {get;private set;}=SkywellToken.Rain;
        public bool WeatherSelectionHeld {get;private set;}
        public bool WeatherPickerOpen=>WeatherSelectionHeld&&Time.unscaledTime-pickerStarted>=weatherHoldTime;
        public bool WeatherTargeting {get;private set;}
        public SkywellWeatherTarget HoveredWeatherTarget {get;private set;}
        public bool WeatherInputBlocked=>WeatherTargeting||WeatherSelectionHeld||weatherClosedFrame==Time.frameCount;
        private bool pointerPicker,primaryPending;
        private float pickerStarted,primaryStarted;
        private int weatherClosedFrame=-1;
        private string weatherCastFeedback;
        private float weatherCastFeedbackUntil;
        private bool FootAirborne=>grove&&grove.Ride&&grove.Ride.FootDriver&&grove.Ride.FootDriver.IsAirborne;
        private bool WeatherTravelBlocked=>FootAirborne||grove&&grove.Ride&&grove.Ride.IsMounted||journey&&journey.ControlsMovement||voyage&&voyage.Phase>=SkywellVoyagePhase.Boarding&&voyage.Phase<=SkywellVoyagePhase.Docking;

        public void ConfigureWeatherTargets(SkywellWeatherTarget[] targets)
        {CancelWeatherTargeting();weatherTargets=targets??Array.Empty<SkywellWeatherTarget>();}
        public bool CanTargetWeather(SkywellWeatherTarget target)
        {
            if(!target||!target.isActiveAndEnabled||Array.IndexOf(weatherTargets,target)<0||!player||!CanUseToken(SelectedWeather))return false;
            if(ObservationInputBlocked||MarketInputBlocked||WeatherTravelBlocked||!MarketWeatherAllowed(target.StationIndex,SelectedWeather))return false;
            var station=WeatherStation(target.StationIndex);
            if(!station||!station.isActiveAndEnabled||!station.Zone||!station.Zone.isActiveAndEnabled||target.SurfaceDistance(player.FeetPosition)>weatherCastRange)return false;
            if(!station.Zone.Contains(player.FeetPosition)&&!target.HasVisibleSurface(cameraRig?cameraRig.ViewCamera:null,player.FeetPosition,weatherCastRange))return false;
            return target.StationIndex!=TowerStation||!DoorwayOccupied();
        }
        // Weather volumes describe where machines receive weather, not where a person must stand to aim.
        // A cast made outside its volume keeps a small, stationary operating area; walking away returns the token.
        private void RecordWeatherOperatingArea(int station)
        {
            if(remoteWeatherActive.Length!=consoles.Length)
            {remoteWeatherActive=new bool[consoles.Length];remoteWeatherOrigins=new Vector3[consoles.Length];}
            remoteWeatherActive[station]=!consoles[station].Zone.Contains(player.FeetPosition);
            remoteWeatherOrigins[station]=player.FeetPosition;
        }
        private bool WithinWeatherOperatingArea(int station)
        {
            var zone=consoles[station].Zone;
            // A remote cast stays anchored to its original operating point even if the player
            // subsequently enters the target volume. Local casts keep their existing zone lifetime.
            if(station>=remoteWeatherActive.Length||!remoteWeatherActive[station])return zone&&zone.Contains(player.FeetPosition);
            if(Vector3.Distance(player.FeetPosition,remoteWeatherOrigins[station])>remoteWeatherMoveRadius)return false;
            foreach(var target in weatherTargets)
                if(target&&target.isActiveAndEnabled&&target.StationIndex==station&&target.SurfaceDistance(player.FeetPosition)<=weatherCastRange)return true;
            return false;
        }
        public string TargetingPrompt
        {
            get
            {
                if(Time.unscaledTime<weatherCastFeedbackUntil)return weatherCastFeedback+" · 松开 E 返回";
                if(!CanUseToken(SelectedWeather))return "晴令在船上，需要先拾取 · 松开 E 返回";
                if(HoveredWeatherTarget)
                {
                    int station=HoveredWeatherTarget.StationIndex;
                    string controls=SelectedWeather!=SkywellToken.Wind?"左键"+WeatherActionLabels(station,SelectedWeather)[0]:
                        station==TowerStation||station==CollectorStation?"左键顺时针 · 右键逆时针":
                        station==SkywellDeerGrove.StationIndex?"左键或右键摇落苹果":"左键向东 · 右键向西";
                    return HoveredWeatherTarget.DisplayName+" · "+controls+" · 松开 E 返回";
                }
                foreach(var target in weatherTargets)if(CanTargetWeather(target))return SelectedWeather==SkywellToken.Wind?
                    "指向高亮物体 · 左键顺时针 / 右键逆时针 · 松开 E 返回":"左键点击高亮物体施令 · 松开 E 返回";
                return "此处没有可施令的物体 · 看向附近未被遮挡的目标";
            }
        }
        public bool TryCastWeather(SkywellWeatherTarget target,bool clockwise=true)
        {
            if(!WeatherTargeting||!CanTargetWeather(target)||!clockwise&&SelectedWeather!=SkywellToken.Wind)return false;
            var station=WeatherStation(target.StationIndex);
            if(station.Busy){weatherCastFeedback="天气切换中，稍候再施令。";weatherCastFeedbackUntil=Time.unscaledTime+1;return false;}
            // Each button has a fixed direction, independent of previous casts or weather changes.
            int action=SelectedWeather==SkywellToken.Wind&&clockwise?1:0;
            bool applied=TryApplyWeather(target.StationIndex,SelectedWeather,action);
            weatherCastFeedback=message;weatherCastFeedbackUntil=Time.unscaledTime+1.3f;
            return applied;
        }
        public void BeginPointerWeatherPicker()=>BeginWeatherPicker(true);
        private void BeginWeatherPicker(bool pointer)
        {
            if(ObservationInputBlocked||MarketInputBlocked||primaryPending||WeatherInputBlocked||WeatherTravelBlocked)return;
            WeatherSelectionHeld=true;pointerPicker=pointer;pickerStarted=Time.unscaledTime;WeatherCandidate=SelectedWeather;
            SyncCameraInteraction();
        }
        public void ChooseWeatherCandidate(SkywellToken token)
        {if(WeatherPickerOpen&&token>=SkywellToken.Rain&&token<=SkywellToken.Sun)WeatherCandidate=token;}
        public void CancelWeatherPicker()
        {
            if(WeatherSelectionHeld)weatherClosedFrame=Time.frameCount;
            WeatherSelectionHeld=false;WeatherCandidate=SelectedWeather;
            SyncCameraInteraction();
        }
        public void CancelWeatherTargeting()
        {
            if(WeatherTargeting||primaryPending)weatherClosedFrame=Time.frameCount;
            primaryPending=WeatherTargeting=false;HoveredWeatherTarget=null;weatherCastFeedbackUntil=0;
            foreach(var target in weatherTargets)if(target)target.SetHighlight(false,false);
        }
        private void BeginPrimaryPress()
        {
            if(WeatherInputBlocked||primaryPending||WeatherTravelBlocked&&!(grove&&grove.Ride&&grove.Ride.IsMounted))return;
            primaryPending=true;primaryStarted=Time.unscaledTime;
        }
        private void UpdateWeatherInput()
        {
            var k=Keyboard.current;
            if(ObservationInputBlocked||MarketInputBlocked)
            {
                CancelWeatherTargeting();CancelWeatherPicker();
                if(ObservationOpen&&k!=null&&k.escapeKey.wasPressedThisFrame)CloseObservation();
                SyncCameraInteraction();
                return;
            }
            if(k!=null&&k.tabKey.wasPressedThisFrame)BeginWeatherPicker(false);
            if(WeatherSelectionHeld)
            {
                bool held=pointerPicker?Mouse.current!=null&&Mouse.current.leftButton.isPressed:k!=null&&k.tabKey.isPressed;
                if(k!=null&&k.escapeKey.wasPressedThisFrame)CancelWeatherPicker();
                else if(!held)
                {
                    if(WeatherPickerOpen)SelectedWeather=WeatherCandidate;
                    CancelWeatherPicker();
                }
                else if(WeatherPickerOpen&&k!=null)
                {
                    if(k.digit1Key.wasPressedThisFrame)WeatherCandidate=SkywellToken.Rain;
                    if(k.digit2Key.wasPressedThisFrame)WeatherCandidate=SkywellToken.Wind;
                    if(k.digit3Key.wasPressedThisFrame)WeatherCandidate=SkywellToken.Sun;
                    if(k.leftArrowKey.wasPressedThisFrame)WeatherCandidate=(SkywellToken)(1+((int)WeatherCandidate+1)%3);
                    if(k.rightArrowKey.wasPressedThisFrame)WeatherCandidate=(SkywellToken)(1+(int)WeatherCandidate%3);
                }
            }
            if(primaryPending)
            {
                if(k==null||k.escapeKey.wasPressedThisFrame||WeatherTravelBlocked&&!(grove&&grove.Ride&&grove.Ride.IsMounted))CancelWeatherTargeting();
                else if(!k.eKey.isPressed)
                {
                    bool shortPress=!WeatherTargeting&&Time.unscaledTime-primaryStarted<weatherHoldTime;
                    CancelWeatherTargeting();
                    if(shortPress){weatherClosedFrame=-1;PerformPrimaryInteraction();}
                }
                else if(Time.unscaledTime-primaryStarted>=weatherHoldTime)
                {
                    if(grove&&grove.Ride&&grove.Ride.IsMounted){CancelWeatherTargeting();SyncCameraInteraction();return;}
                    WeatherTargeting=true;
                    SyncCameraInteraction();
                    UpdateWeatherTargets();
                }
            }
            SyncCameraInteraction();
            var pointer=Mouse.current;
            if(cameraRig&&!cameraRig.InputSuppressed&&pointer!=null&&pointer.leftButton.wasPressedThisFrame
                &&(k==null||!k.escapeKey.wasPressedThisFrame)&&!SkywellHUD.IsWeatherSelectorHit(pointer.position.ReadValue()))
                cameraRig.ResumeShoulderLook();
        }
        // L5 owns its modal state; the shared camera owns the cursor and gameplay pose.
        private void SyncCameraInteraction()
        {
            if(!cameraRig)return;
            bool blocked=WeatherInputBlocked||ObservationInputBlocked||MarketInputBlocked;
            cameraRig.InputSuppressed=blocked;
            cameraRig.SetPointerInteraction(blocked);
            bool frameTargets=WeatherTargeting&&cameraRig.IsShoulderView&&!ObservationInputBlocked&&!MarketInputBlocked;
            if(frameTargets&&!cameraRig.InteractionFramingActive)
            {
                Bounds bounds=new Bounds(player.FeetPosition+Vector3.up,Vector3.one*2);
                bool found=false;
                foreach(var target in weatherTargets)
                    if(CanTargetWeather(target)&&target.TryGetFramingBounds(player.FeetPosition.y,out Bounds targetBounds))
                    {bounds.Encapsulate(targetBounds);found=true;}
                cameraRig.SetInteractionFraming(found,bounds);
            }
            else if(!frameTargets)cameraRig.SetInteractionFraming(false);
        }
        private void UpdateWeatherTargets()
        {
            HoveredWeatherTarget=null;float nearest=float.PositiveInfinity;
            var mouse=Mouse.current;var camera=cameraRig?cameraRig.ViewCamera:null;
            if(mouse!=null&&camera&&!SkywellHUD.IsWeatherSelectorHit(mouse.position.ReadValue()))
            {
                Vector2 point=mouse.position.ReadValue();
                if(camera.pixelRect.Contains(point))
                {
                    Ray ray=camera.ScreenPointToRay(point);
                    foreach(var target in weatherTargets)
                        if(CanTargetWeather(target)&&target.TryHit(ray,out float distance)&&distance<nearest)
                        {nearest=distance;HoveredWeatherTarget=target;}
                }
            }
            foreach(var target in weatherTargets)if(target)target.SetHighlight(CanTargetWeather(target),target==HoveredWeatherTarget);
            if(mouse!=null&&HoveredWeatherTarget)
            {
                if(mouse.leftButton.wasPressedThisFrame)TryCastWeather(HoveredWeatherTarget);
                else if(mouse.rightButton.wasPressedThisFrame&&SelectedWeather==SkywellToken.Wind)TryCastWeather(HoveredWeatherTarget,false);
            }
        }
        private void ResetWeatherInput()
        {
            CancelWeatherTargeting();CancelWeatherPicker();
            Array.Clear(remoteWeatherActive,0,remoteWeatherActive.Length);
            SelectedWeather=WeatherCandidate=SkywellToken.Rain;
            SyncCameraInteraction();
        }
        private void OnDisable()
        {
            CloseObservation();ResetWeatherInput();
            if(cameraRig){cameraRig.InputSuppressed=false;cameraRig.SetPointerInteraction(true);}
        }
        private void OnApplicationFocus(bool focused)
        {if(!focused){CloseObservation();CancelWeatherPicker();CancelWeatherTargeting();SyncCameraInteraction();}}
    }
}
