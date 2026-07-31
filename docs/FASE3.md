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

1. Una lista de mandos detectados por `RawGameController`, con su nombre,
   VID/PID, número de ejes, botones y switches (D-Pad). Por ejemplo, un
   DualSense o un mando Xbox típico mostrará algo como
   `Ejes=6 Botones=... Switches(D-Pad)=1` (6 ejes = stick izq X/Y, stick
   der X/Y, gatillo izq, gatillo der — el orden exacto lo confirmamos
   moviéndolos, no lo des por hecho).
2. Una segunda lista (`Windows.Gaming.Input.Gamepad`) — normal que aparezca
   vacía si tu mando no es un Xbox/compatible XInput; no es un error.
3. Al elegir el número del mando, entra en modo monitor en vivo:

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

   Este panel se refresca varias veces por segundo.

## 6. Qué comprobar exactamente (anota los resultados)

- [ ] El mando aparece en la lista con un nombre reconocible.
- [ ] Mueve el **joystick izquierdo** de izquierda a derecha: anota qué
      `axis[N]` cambia y en qué sentido (¿sube hacia 1.0 al mover a la
      derecha, o baja hacia 0.0?).
- [ ] Mueve el joystick izquierdo de arriba a abajo: anota qué `axis[N]`
      cambia.
- [ ] Mueve el **joystick derecho** izq/der y arriba/abajo: anota los
      `axis[N]` correspondientes.
- [ ] Presiona algunos botones y confirma que aparecen en "BOTONES
      pulsados" con su índice.
- [ ] Si tu mando tiene D-Pad, muévelo y confirma que `switch[0]` cambia
      (Up, Down, Left, Right, UpLeft, etc.).
- [ ] Suelta ambos joysticks: confirma que sus ejes vuelven a un valor
      estable cercano (no necesariamente exacto) a 0.5 — esto será la base
      de la deadzone en la Fase 4.

Con esa tabla de "qué axis[N] es cada cosa" para tu mando concreto,
construimos en la Fase 4 la pantalla de asignación manual (y unos valores
por defecto razonables para mandos Xbox/PlayStation típicos).

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
