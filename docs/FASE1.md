# FASE 1 — Herramienta de diagnóstico UVC/DirectShow

Objetivo de esta fase: confirmar, con hardware real, que se puede leer y
modificar Pan/Tilt/Zoom (y Roll) de la Osmo Pocket 3 a través de
`IAMCameraControl`, **sin** cerrar OBS ni apropiarse del stream de vídeo.

No se avanza a la Fase 2 hasta que confirmes que esto funciona en tu PC.

## 1. Qué instalar

1. **.NET 8 SDK (x64)** para Windows:
   https://dotnet.microsoft.com/download/dotnet/8.0
   Instala el **SDK** (no solo el Runtime). Verifica en una terminal
   (PowerShell o CMD):

   ```powershell
   dotnet --version
   ```

   Debería mostrar `8.0.x`.

2. No hace falta instalar Visual Studio completo, pero si lo prefieres puedes
   abrir `DjiPtzController.sln` directamente en Visual Studio 2022 (con la
   carga de trabajo ".NET desktop development").

No hace falta instalar DirectShowLib manualmente: es un paquete NuGet
(`DirectShowLib.Standard`) ya referenciado en `src/DjiPtz.Core/DjiPtz.Core.csproj`;
se descarga solo al compilar (necesitas conexión a internet la primera vez).

## 2. Dónde poner los archivos

Todo el código ya está en este repositorio, con esta estructura (no la
cambies, las fases siguientes reutilizan `DjiPtz.Core`):

```
DjiPtzController.sln
src/DjiPtz.Core/...            <- librería de acceso a DirectShow/UVC
src/DjiPtz.Diagnostics/...     <- Fase 1: consola de diagnóstico
```

En tu PC con Windows, simplemente clona o descarga este repositorio (rama
`claude/dji-pocket3-ptz-controller-xfv1rs`) en una carpeta, por ejemplo:

```
C:\Dev\DjiPtzController\
```

```powershell
git clone -b claude/dji-pocket3-ptz-controller-xfv1rs https://github.com/diegoorsc/app-joystick-osmo-p-.git C:\Dev\DjiPtzController
cd C:\Dev\DjiPtzController
```

## 3. Cómo compilar

Desde PowerShell, en la carpeta del repositorio:

```powershell
dotnet build DjiPtzController.sln
```

Debe terminar con `Build succeeded` y 0 errores. La primera vez tardará un
poco más porque descarga el paquete NuGet `DirectShowLib.Standard`.

(Este mismo código ya se compiló sin errores ni warnings en el entorno de
desarrollo antes de entregártelo — pero como las APIs DirectShow/COM que usa
solo existen en Windows, la ejecución solo se puede probar en tu PC.)

## 4. Cómo ejecutar

1. Conecta la Osmo Pocket 3 por USB-C y ponla en **modo Webcam**.
2. (Opcional pero recomendado para la Fase 2) Abre OBS Studio y añade la
   Pocket 3 como "Dispositivo de captura de vídeo", para comprobar en el
   mismo momento que ambos programas conviven.
3. Ejecuta la herramienta de diagnóstico:

   ```powershell
   dotnet run --project src/DjiPtz.Diagnostics
   ```

   o, tras compilar, directamente el ejecutable:

   ```powershell
   src\DjiPtz.Diagnostics\bin\Debug\net8.0-windows\DjiPtzDiagnostics.exe
   ```

## 5. Qué deberías ver

1. Una lista numerada de cámaras detectadas por DirectShow (debería aparecer
   algo como `DJI Osmo Pocket 3` o similar; si Windows la nombra distinto, la
   verás igualmente en la lista).
2. Si el nombre contiene "DJI", "Pocket" u "Osmo", se preselecciona sola;
   si no, escribe el número que le corresponda.
3. Un mensaje indicando que se ha enlazado con la cámara **sin abrir el
   stream de vídeo**.
4. Una tabla con las propiedades de `IAMCameraControl`:

   ```
   Propiedad      Min      Max   Step  Default  Current  Flags        Estado
   Pan           ...      ...    ...      ...      ...   Manual       OK
   Tilt          ...      ...    ...      ...      ...   Manual       OK
   Roll          ...      ...    ...      ...      ...   Manual       OK
   Zoom          ...      ...    ...      ...      ...   Manual       OK
   Exposure       -        -      -        -        -    -            No soportado (...)
   Iris           -        -      -        -        -    -            No soportado (...)
   Focus          -        -      -        -        -    -            No soportado (...)
   ```

   Los valores reales de Min/Max/Step/Default los reporta la propia cámara —
   no están inventados en el código. Es normal que Exposure/Iris/Focus
   aparezcan como "No soportado" en la Pocket 3; lo importante para este
   proyecto es que **Pan, Tilt y Zoom (y si existe, Roll) devuelvan `OK`**.

5. Un prompt interactivo `>` donde puedes probar:

   ```
   > get Zoom
   > set Pan 3
   > set Tilt -2
   > set Zoom 5
   > refresh
   > quit
   ```

   Los valores que le pases a `set` deben estar dentro del rango
   `[Min, Max]` que mostró la tabla, y respetar el `Step` (incremento
   permitido) que reporta la cámara — pruébalo primero con valores cercanos
   al `Default` y observa físicamente el gimbal.

## 6. Qué comprobar exactamente

- [ ] La Pocket 3 aparece en la lista de cámaras.
- [ ] `Pan`, `Tilt` y `Zoom` aparecen como `OK` con rangos Min/Max/Step/Default
      reales (no vacíos).
- [ ] Al ejecutar `set Pan <valor>` el gimbal físico gira horizontalmente.
- [ ] Al ejecutar `set Tilt <valor>` el gimbal físico inclina.
- [ ] Al ejecutar `set Zoom <valor>` el zoom cambia (si la Pocket 3 expone
      zoom óptico/digital vía UVC — confírmalo viendo la imagen en OBS).
- [ ] **Mientras haces todo esto, OBS Studio sigue mostrando la imagen de la
      cámara sin cortes ni errores** (esto confirma que no estamos tomando
      el dispositivo en modo exclusivo).

## 7. Si algo falla

Antes de cambiar de tecnología, dime exactamente:

1. Si la cámara aparece o no en la lista (y con qué nombre exacto).
2. Si `IAMCameraControl soportado` sale `True` o `False`.
3. La tabla completa que imprime el programa (captura de pantalla o texto).
4. Si `dotnet build` da algún error, el texto completo del error.
5. Si al hacer `set Pan ...` obtienes un `ERROR:` en consola, el mensaje
   completo (incluye normalmente un HRESULT que ayuda a diagnosticar).

Con eso decidimos si el problema es de permisos, de nombres de propiedad, de
que la Pocket 3 exponga Pan/Tilt por una interfaz distinta (por ejemplo,
XU/Extension Unit en vez de `IAMCameraControl` estándar), etc. — sin asumir
nada de antemano.

## 8. Notas técnicas (por qué no interfiere con OBS)

`CameraControlService.Open` enlaza el `IMoniker` del dispositivo directamente
a `IBaseFilter` (`IMoniker.BindToObject`), y desde ahí hace un cast a
`IAMCameraControl` / `IAMVideoProcAmp`. **No** se añade el filtro a un
`IFilterGraph2`, no se conectan pines y no se llama a `IMediaControl.Run`.
Eso es exactamente lo que abre el stream de captura en exclusiva; al no
hacerlo, el filtro solo se usa como "canal de control" — igual que hace el
propio diálogo de Windows/OBS "Control de cámara", que puedes tener abierto
a la vez sin que se pisen.

Cuando escribes `quit`, se libera el filtro (`Marshal.ReleaseComObject`) de
forma ordenada.
