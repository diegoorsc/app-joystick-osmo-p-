# DJI Pocket 3 PTZ Controller

Aplicación para Windows 11 que convierte una DJI Osmo Pocket 3 (conectada por
USB-C en modo Webcam) en una cámara PTZ controlable con un gamepad de dos
joysticks, usando los controles UVC/DirectShow (`IAMCameraControl` /
`IAMVideoProcAmp`) que Windows ya expone para Pan, Tilt, Zoom y Roll — los
mismos que se ven en OBS Studio en
`Propiedades del dispositivo de captura → Configurar vídeo → Control de cámara`.

El proyecto se desarrolla por fases. Cada fase añade una capa nueva sobre la
anterior y no se avanza a la siguiente hasta confirmar que la anterior
funciona con hardware real.

## Fases

- **Fase 1 — Diagnóstico UVC/DirectShow** ✅ verificada con hardware real
  Herramienta de consola que enumera cámaras, localiza la Pocket 3, lista los
  rangos de `IAMCameraControl` (Pan/Tilt/Roll/Zoom/Exposure/Iris/Focus) y
  permite moverlos manualmente sin cerrar OBS. Ver [`docs/FASE1.md`](docs/FASE1.md).
- **Fase 2 — OBS + control simultáneo** ✅ verificada con hardware real
  Confirmado: OBS sigue capturando la Pocket 3 sin cortes mientras la
  herramienta de la Fase 1 mueve Pan/Tilt/Zoom.
- **Fase 3 — Detección de gamepad y lectura de ejes** ✅ verificada con hardware real
  Consola que detecta mandos Xbox/PlayStation/genéricos vía
  `Windows.Gaming.Input.RawGameController`/`Gamepad` y muestra sus ejes/botones
  en vivo. Ver [`docs/FASE3.md`](docs/FASE3.md).
- **Fase 4 — Mouse + teclado → Pan/Tilt/Zoom** 🔄 en verificación
  Ya no requiere gamepad: la rueda del mouse mueve el Pan (cada "click" de
  scroll desplaza el Pan una cantidad configurable, con barrido suave hasta
  llegar), las flechas Arriba/Abajo del teclado mueven el Tilt mientras se
  mantengan pulsadas (jog, como un joystick) y las teclas +/- controlan el
  Zoom de la misma forma. Incluye Center Gimbal / Reset Zoom e inversión de
  ejes. Ver [`docs/FASE4.md`](docs/FASE4.md).
- **Fase Web — Control remoto desde el celular** 🔄 en verificación
  No requiere Raspberry Pi ni gamepad: el PC con la cámara conectada levanta
  una pagina web en la red local (mismo WiFi), con un joystick táctil para
  Pan/Tilt y botones +/- para Zoom, pensada para abrirse desde el navegador
  de un celular. Ver [`docs/WEBCONTROL.md`](docs/WEBCONTROL.md).
- **Fase 5** — Interfaz gráfica (WPF).
- **Fase 6** — Presets PTZ, integración con Stream Deck, funciones avanzadas.

## Estructura del repositorio

```
DjiPtzController.sln
src/
  DjiPtz.Core/            Librería compartida: acceso DirectShow/UVC y gamepad
                           (reutilizada por todas las fases siguientes)
    DirectShow/
      NativeMethods.cs         P/Invoke a ole32.dll (CreateBindCtx)
      CameraDeviceInfo.cs      Envoltorio de un DsDevice (cámara detectada)
      CameraEnumerator.cs      Enumeración de cámaras UVC/WDM
      CameraControlRange.cs    DTOs de rango (Min/Max/Step/Default/Flags)
      CameraControlService.cs Acceso a IAMCameraControl / IAMVideoProcAmp
    Gamepad/
      GamepadEnumerator.cs     Enumeración de mandos (RawGameController / XInput)
      GamepadSnapshot.cs       DTO de una lectura instantánea de ejes/botones
      GamepadReader.cs         Lectura tipada de un RawGameController
      PtzStickFrame.cs         Lectura unificada de sticks (-1.0 a 1.0)
      IPtzStickSource.cs / XInputStickSource.cs / RawStickSource.cs
    Control/
      AxisDeadzoneCurve.cs     Deadzone + curva progresiva/exponencial
      PtzAxisController.cs     Integra velocidad -> posición absoluta (jog y seek suave)
  DjiPtz.Diagnostics/     Fase 1: herramienta de consola de diagnóstico de cámara
    Program.cs
  DjiPtz.GamepadDiagnostics/  Fase 3: herramienta de consola de diagnóstico de gamepad
    Program.cs
  DjiPtz.PtzControl/      Fase 4: control en vivo por mouse + teclado (cámara)
    Program.cs
    Input/
      MouseWheelHook.cs      Hook WH_MOUSE_LL: rueda del mouse -> Pan
      NativeKeyboard.cs      GetAsyncKeyState: flechas -> Tilt, +/- -> Zoom
  DjiPtz.WebControl/      Fase Web: pagina de control remoto para celular
    Program.cs               Servidor Kestrel + loop de control (misma
                              cámara, mismo PtzAxisController/AxisDeadzoneCurve)
    WebUi.cs                 HTML/CSS/JS embebidos (joystick táctil + zoom)
docs/
  FASE1.md                Instrucciones detalladas de la Fase 1
  FASE3.md                Instrucciones detalladas de la Fase 3
  FASE4.md                Instrucciones detalladas de la Fase 4
  WEBCONTROL.md            Instrucciones de la Fase Web (control desde celular)
```

## Requisitos generales

- Windows 11 (las APIs DirectShow/COM usadas aquí no existen en Linux/macOS).
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`dotnet-sdk-8.0`, x64).
- DJI Osmo Pocket 3 conectada por USB-C en modo **Webcam**.

Ver `docs/FASE1.md` para las instrucciones paso a paso de la fase actual.
