# FASE 3 — Detección de gamepad y lectura de ejes

Objetivo: confirmar que Windows detecta tu mando (PlayStation, Xbox o
genérico) y que podemos leer en tiempo real todos sus ejes/botones, para
poder asignarlos manualmente en la Fase 4 (Pan/Tilt/Zoom).

No se avanza a la Fase 4 hasta que confirmes que esto funciona con tu mando
real.

## Qué API se usó y por qué

Se usa `Windows.Gaming.Input.RawGameController`, la API de Windows que mejor
cubre **a la vez** mandos Xbox, PlayStation (DualShock4/DualSense, por USB o
Bluetooth) y mandos genéricos: expone los ejes "en crudo" (sin asumir cuál es
el joystick izquierdo o derecho), que es exactamente lo que pediste — ver
todos los ejes y asignarlos tú mismo.

`Windows.Gaming.Input.Gamepad` (la API específica de XInput, solo mandos
compatibles Xbox) también se lista de forma informativa, pero el mapeo de
ejes de la Fase 4 se construirá sobre `RawGameController` porque es la única
que funciona igual de bien con los tres tipos de mando.

No hace falta instalar SDL2, DirectInput ni ningún driver adicional: es una
API integrada en Windows 10/11.

**Nota técnica (bug corregido):** `Windows.Gaming.Input` puebla su lista de
dispositivos mediante notificaciones internas que necesitan un *bucle de
mensajes de Windows* para entregarse. Una consola "pura" no tiene ese bucle,
así que la primera versión de esta herramienta podía quedarse
permanentemente en `0` mandos detectados aunque el mando estuviera
perfectamente reconocido por Windows (esto es lo que le pasó al usuario con
un mando PS4 que sí funcionaba bien en Windows). Se solucionó marcando el
programa como `[STAThread]` y usando `System.Windows.Forms.Application.DoEvents()`
solo para bombear mensajes (sin mostrar ninguna ventana), reintentando la
enumeración durante ~3 segundos antes de darla por vacía.

## 1. Qué instalar

Nada nuevo respecto a la Fase 1 (mismo .NET 8 SDK). Como esta fase usa APIs
de Windows Runtime, el proyecto `DjiPtz.Core` y los ejecutables ahora
apuntan al TFM `net8.0-windows10.0.19041.0` en vez de `net8.0-windows` — ya
está así en el código, no tienes que cambiar nada.

## 2. Archivos nuevos

```
src/DjiPtz.Core/Gamepad/
  GamepadEnumerator.cs     Lista mandos (RawGameController y XInput Gamepad)
  GamepadSnapshot.cs       DTO de una lectura instantánea (ejes/botones/switches)
  GamepadReader.cs         Envuelve un RawGameController y da lecturas tipadas
src/DjiPtz.GamepadDiagnostics/
  DjiPtz.GamepadDiagnostics.csproj
  Program.cs               Fase 3: consola de monitorización en vivo
```

Ya están en el repositorio (rama `claude/dji-pocket3-ptz-controller-xfv1rs`).
Actualiza tu copia local:

```powershell
cd C:\Dev\DjiPtzController
git pull origin claude/dji-pocket3-ptz-controller-xfv1rs
```

## 3. Cómo compilar

```powershell
dotnet build DjiPtzController.sln
```

Debe compilar los tres proyectos (`DjiPtz.Core`, `DjiPtz.Diagnostics`,
`DjiPtz.GamepadDiagnostics`) sin errores. Este código ya se verificó aquí
(compilación cruzada) sin errores ni warnings; la prueba con tu mando real
la haces tú.

## 4. Cómo ejecutar

1. Conecta tu mando por USB, o emparéjalo por Bluetooth desde
   Configuración de Windows → Bluetooth y dispositivos.
2. Pulsa algún botón del mando para que Windows lo detecte como activo.
3. Ejecuta:

   ```powershell
   dotnet run --project src/DjiPtz.GamepadDiagnostics
   ```

## 5. Qué deberías ver

1. Una lista de mandos detectados por `RawGameController` (prefijo `R`), con
   su nombre, VID/PID, número de ejes, botones y switches (D-Pad).
2. Una segunda lista de mandos detectados por `Windows.Gaming.Input.Gamepad`
   / XInput (prefijo `X`).

   **Importante:** un mismo mando físico aparece en **una sola** de las dos
   listas, nunca en ambas — Windows lo enruta por una vía u otra según cómo
   se identifique el hardware ante el sistema:
   - Mandos Xbox y muchos mandos genéricos "compatibles Xbox" → aparecen
     solo como `[X#]` (XInput). Es el caso más común y, de hecho, el más
     cómodo: XInput ya te da los ejes con nombre (stick izquierdo/derecho,
     gatillos) sin tener que adivinar índices.
   - Mandos PlayStation nativos (DualShock4/DualSense sin emulación XInput)
     y mandos genéricos "puramente HID" → aparecen solo como `[R#]`
     (RawGameController), con ejes numerados `axis[0]`, `axis[1]`, ...

   Si tu mando aparece en `[X#]`, escribe por ejemplo `X0` cuando el
   programa te lo pida. Si aparece en `[R#]`, escribe `R0`.
3. Al elegir el mando, entra en modo monitor en vivo. Si es un mando `[R#]`
   (RawGameController) verás:

   ```
   EJES  (rango 0.0 - 1.0; centrado ~0.5):
     axis[0] = 0.502  [####################....................]
     axis[1] = 0.498  [###################.....................]
     axis[2] = 0.501  [####################....................]
     axis[3] = 0.503  [####################....................]
     axis[4] = 0.000  [........................................]
     axis[5] = 0.000  [........................................]

   BOTONES pulsados:
     (ninguno)

   SWITCHES / D-Pad:
     switch[0] = Center
   ```

   Si en cambio es un mando `[X#]` (XInput) verás los ejes ya identificados
   por nombre:

   ```
   STICK IZQUIERDO  (rango -1.0 a 1.0, centrado en 0.0):
     X =   0.012  [####################....................]
     Y =  -0.008  [####################....................]

   STICK DERECHO  (rango -1.0 a 1.0, centrado en 0.0):
     X =   0.000  [####################....................]
     Y =   0.000  [####################....................]

   GATILLOS  (rango 0.0 a 1.0):
     LT =   0.000  [........................................]
     RT =   0.000  [........................................]

   BOTONES: None
   ```

   Este panel se refresca varias veces por segundo.

## 6. Qué comprobar exactamente (anota los resultados)

- [ ] El mando aparece en la lista (`[R#]` o `[X#]`) con un nombre
      reconocible.
- Si es un mando **`[X#]` (XInput)** — como el tuyo por USB:
  - [ ] Mueve el joystick izquierdo: confirma que `STICK IZQUIERDO X` e `Y`
        cambian de signo/valor según la dirección (derecha → X positivo,
        arriba → Y positivo es lo habitual, confírmalo con tus ojos).
  - [ ] Mueve el joystick derecho: confirma que `STICK DERECHO X`/`Y`
        cambian.
  - [ ] Pulsa los gatillos: confirma que `LT`/`RT` suben hacia 1.0.
  - [ ] Suelta ambos joysticks: confirma que X e Y vuelven cerca de `0.000`.
  - [ ] Presiona botones y confirma que aparecen listados en `BOTONES:`.
- Si es un mando **`[R#]` (RawGameController)**:
  - [ ] Mueve el joystick izquierdo izq/der y arriba/abajo: anota qué
        `axis[N]` cambia en cada caso y en qué sentido.
  - [ ] Mueve el joystick derecho: anota los `axis[N]` correspondientes.
  - [ ] Presiona botones y confirma que aparecen en "BOTONES pulsados".
  - [ ] Si tiene D-Pad, confirma que `switch[0]` cambia (Up/Down/Left/Right).
  - [ ] Suelta ambos joysticks: confirma que sus ejes vuelven cerca de
        `0.500`.

Con esa información (qué vía usa tu mando, y qué eje/índice es cada cosa),
construimos en la Fase 4 la pantalla de asignación manual con valores por
defecto razonables tanto para mandos XInput como RawGameController.

## 7. Si algo falla

Dime, sin que yo asuma nada:

1. Si el mando aparece o no en la lista `RawGameController` (y con qué
   nombre/VID/PID).
2. Si al moverlo ves algún `axis[N]` cambiar, o si todos quedan fijos en 0.
3. El texto completo si `dotnet build` o `dotnet run` dan error.
4. Si usas Bluetooth: si el mando aparece "emparejado y conectado" en
   Configuración de Windows.

Con eso decidimos si hace falta algún ajuste (por ejemplo mandos que
necesitan estar en "modo XInput" o "modo DirectInput" mediante un switch
físico, algo común en mandos genéricos baratos) antes de seguir.
