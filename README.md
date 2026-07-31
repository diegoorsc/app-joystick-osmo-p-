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

- **Fase 1 — Diagnóstico UVC/DirectShow** ✅ (código en este repo)
  Herramienta de consola que enumera cámaras, localiza la Pocket 3, lista los
  rangos de `IAMCameraControl` (Pan/Tilt/Roll/Zoom/Exposure/Iris/Focus) y
  permite moverlos manualmente sin cerrar OBS. Ver [`docs/FASE1.md`](docs/FASE1.md).
- **Fase 2** — Verificación con OBS abierto (usa la misma herramienta de Fase 1).
- **Fase 3** — Detección de gamepad y lectura de ejes.
- **Fase 4** — Mapeo joystick → Pan/Tilt/Zoom (curva progresiva, deadzone).
- **Fase 5** — Interfaz gráfica (WPF).
- **Fase 6** — Presets PTZ, integración con Stream Deck, funciones avanzadas.

## Estructura del repositorio

```
DjiPtzController.sln
src/
  DjiPtz.Core/            Librería compartida: acceso DirectShow/UVC
                           (reutilizada por todas las fases siguientes)
    DirectShow/
      NativeMethods.cs         P/Invoke a ole32.dll (CreateBindCtx)
      CameraDeviceInfo.cs      Envoltorio de un DsDevice (cámara detectada)
      CameraEnumerator.cs      Enumeración de cámaras UVC/WDM
      CameraControlRange.cs    DTOs de rango (Min/Max/Step/Default/Flags)
      CameraControlService.cs Acceso a IAMCameraControl / IAMVideoProcAmp
  DjiPtz.Diagnostics/     Fase 1: herramienta de consola de diagnóstico
    Program.cs
docs/
  FASE1.md                Instrucciones detalladas de instalación/compilación/uso
```

## Requisitos generales

- Windows 11 (las APIs DirectShow/COM usadas aquí no existen en Linux/macOS).
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`dotnet-sdk-8.0`, x64).
- DJI Osmo Pocket 3 conectada por USB-C en modo **Webcam**.

Ver `docs/FASE1.md` para las instrucciones paso a paso de la fase actual.
