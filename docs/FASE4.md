# FASE 4 — Joystick → Pan/Tilt/Zoom (deadzone + curva progresiva)

Objetivo: mover Pan/Tilt/Zoom de la Pocket 3 con el mando, con
comportamiento de joystick PTZ profesional (centrado = quieto, desviación
pequeña = lento, desviación grande = rápido), sin cerrar OBS.

No se avanza a la Fase 5 (interfaz gráfica) hasta confirmar que esto se
mueve bien con tu mando real.

## Correcciones aplicadas tras la primera prueba con hardware real

- **Zoom por botones T/W (L1 = in, L2 = out)**, como una cámara PTZ
  profesional, en vez de (u opcionalmente además de) el stick derecho. Se
  elige al arrancar el programa.
- **Tilt invertido por defecto**: con el mando/cámara probados, mover el
  stick izquierdo hacia arriba bajaba la cámara y viceversa. Se corrigió
  poniendo `Inverted = true` por defecto en el eje Tilt (se puede volver a
  cambiar en caliente con la tecla `2` si usas otro mando).
- **Movimiento más lento y fluido**: los valores por defecto de velocidad
  eran demasiado rápidos, y además se estaba enviando una posición nueva a
  la cámara cada 20 ms (50 veces por segundo), lo cual es demasiado para el
  motor del gimbal y se veía "a saltos". Ahora el programa integra el
  movimiento internamente a 50 Hz (para que sea preciso) pero solo **envía**
  comandos a la cámara a una frecuencia limitada y configurable (15 Hz por
  defecto) — el gimbal recibe menos órdenes, más espaciadas, y el
  movimiento se ve más fluido. Los valores por defecto de "segundos para
  barrer el rango completo" también subieron de 2.5s/4s a 8s/8s (más
  lento).

## Cómo funciona (arquitectura)

- `DjiPtz.Core/Control/AxisDeadzoneCurve.cs`: convierte la posición cruda
  del joystick (-1.0 a 1.0) en una velocidad normalizada, aplicando la
  deadzone configurable y una curva exponencial progresiva (no escalonada).
- `DjiPtz.Core/Control/PtzAxisController.cs`: la pieza clave. Como la Pocket
  3 solo acepta posiciones **absolutas** por UVC (confirmado en la Fase 1:
  Pan/Tilt/Zoom tienen Min/Max/Step fijos, no hay comando de "velocidad"),
  este controlador integra la velocidad del joystick en incrementos
  pequeños sobre el valor absoluto real, redondeando siempre al `Step` que
  reporta la cámara, y respetando `Min`/`Max`. Esto es lo que hace que se
  sienta como un joystick PTZ de verdad en vez de "saltar" a posiciones.
  También soporta `SeekTowards(valorObjetivo)`, un movimiento suave hacia un
  valor — la misma pieza que usarán "Center Gimbal" y, en la Fase 6, los
  presets.
- `DjiPtz.Core/Gamepad/IPtzStickSource.cs` (+ `XInputStickSource` /
  `RawStickSource`): unifica la lectura de joysticks de la Fase 3 (tanto si
  el mando se detecta como XInput como si se detecta como
  RawGameController) en un único formato `PtzStickFrame` (stick izq/der,
  X/Y, todos en -1.0 a 1.0).
- `DjiPtz.Core/Gamepad/IUnipolarSource.cs` (+ `RawButtonUnipolarSource` /
  `RawAxisUnipolarSource` / `XInputLeftShoulderSource` /
  `XInputLeftTriggerSource` / `ConstantUnipolarSource`): entradas "de un
  solo sentido" (0.0 suelto, 1.0 a fondo) para controlar el Zoom con dos
  botones/gatillos independientes (L1 = in, L2 = out) en vez de un único eje
  bidireccional, como en una cámara PTZ profesional.
- `DjiPtz.PtzControl`: el programa de esta fase. Junta la selección de
  cámara (Fase 1) y de mando (Fase 3), integra el movimiento a ~50 Hz para
  que sea preciso, pero **limita el envío real de comandos a la cámara** a
  una frecuencia configurable (15 Hz por defecto) para que el gimbal se
  mueva de forma fluida en vez de a saltos.

## 1. Qué instalar / archivos nuevos

Nada nuevo que instalar (mismo .NET 8 SDK). Archivos añadidos al repo:

```
src/DjiPtz.Core/Control/
  AxisDeadzoneCurve.cs
  PtzAxisController.cs
src/DjiPtz.Core/Gamepad/
  PtzStickFrame.cs
  IPtzStickSource.cs
  XInputStickSource.cs
  RawStickSource.cs
  IUnipolarSource.cs
  UnipolarSources.cs
src/DjiPtz.PtzControl/
  DjiPtz.PtzControl.csproj
  Program.cs
```

Actualiza tu copia local:

```powershell
cd C:\Dev\DjiPtzController
git pull origin claude/dji-pocket3-ptz-controller-xfv1rs
```

## 2. Cómo compilar

```powershell
dotnet build DjiPtzController.sln
```

## 3. Cómo ejecutar

1. Conecta la Pocket 3 (modo Webcam) y el mando (USB, como en tu última
   prueba que funcionó).
2. (Opcional) Abre OBS con la Pocket 3 capturando, para comprobar en vivo
   que el vídeo no se corta mientras mueves el gimbal.
3. Ejecuta:

   ```powershell
   dotnet run --project src/DjiPtz.PtzControl
   ```

4. Sigue las preguntas:
   - Selecciona la cámara (normalmente se autodetecta).
   - Selecciona el mando (`R0` o `X0`, como en la Fase 3).
   - Si tu mando es `R#` (RawGameController), te pedirá los índices de eje
     para stick izquierdo X/Y y stick derecho X/Y — usa los que anotaste en
     la Fase 3 (por defecto propone 0,1,2,3, el orden más común).
   - Control de Zoom: `1` = botones L1 (in) / L2 (out) (recomendado, Enter
     usa esta opción), `2` = eje Y del stick derecho.
     - Si elegiste `1` y tu mando es `R#`, te pedirá para L1 y para L2 si es
       un botón o un eje/gatillo, y su índice — usa la Fase 3 para
       averiguarlos si no los conoces (pulsa L1/L2 y mira qué `button[N]`
       o `axis[N]` cambia).
     - Si tu mando es `X#` (XInput), usa automáticamente LeftShoulder (L1) y
       LeftTrigger (L2), sin preguntar nada.
   - Deadzone (Enter = 15%), curva/exponente (Enter = 2.5), segundos para
     barrer Pan/Tilt y Zoom a máxima velocidad (Enter = 8s, más alto = más
     lento), y frecuencia de envío a la cámara en Hz (Enter = 15; más bajo
     = más fluido pero menos reactivo).
5. Pulsa una tecla para empezar y mueve los joysticks.

## 4. Qué deberías ver

Un panel que se refresca varias veces por segundo:

```
PAN   min=  -35 max=  215 step=  1  valor=   179  invertido=No
TILT  min=  -90 max=   90 step=  1  valor=     0  invertido=Si
ZOOM  min=  100 max=  400 step=  1  valor=   100  invertido=No

Stick izquierdo:  X=  0.02  Y= -0.01   -> panIn= 0.00  tiltIn= 0.00
Zoom por botones: L1(in)=0.00  L2(out)=0.00   -> zoomIn= 0.00

Deadzone: 15%   Curva (exponente): 2.5   Envio a camara: 15 Hz
Velocidad Pan/Tilt: 23.1 u/s (barrido completo en 8.0s)
Velocidad Zoom:     37.5 u/s (barrido completo en 8.0s)

Teclas: [C] Center Gimbal  [Z] Reset Zoom/1x  [1] Invertir Pan  [2] Invertir Tilt  [3] Invertir Zoom  [Q] Salir
```

(Si elegiste zoom por stick derecho en vez de botones, esa línea se muestra
como `Stick derecho: X=... Y=... -> zoomIn=...` en su lugar.)

## 5. Qué comprobar exactamente

- [ ] Con el joystick **izquierdo centrado**, `valor` de PAN y TILT no
      cambian (quietos).
- [ ] Desviar el stick izquierdo **un poco** hacia un lado: el `valor` de
      PAN cambia **lentamente**.
- [ ] Desviar el stick izquierdo **al máximo**: PAN cambia **rápido**.
- [ ] El gimbal físico se mueve acorde en cada caso (compruébalo mirando la
      cámara, no solo los números).
- [ ] Repite lo mismo con el eje Y del stick izquierdo para TILT (ahora
      debería ir en el sentido correcto: arriba = tilt arriba).
- [ ] Mantén pulsado L1: el ZOOM debe acercar progresivamente mientras lo
      mantengas. Mantén pulsado L2: debe alejar (si tu mando lo tiene como
      gatillo analógico, cuanto más lo aprietes más rápido debería ir).
- [ ] Confirma en OBS que el zoom físico cambia de verdad.
- [ ] Si algún eje se mueve **al revés** de lo esperado, pulsa `1`/`2`/`3`
      para invertirlo y confirma que se corrige.
- [ ] Confirma que ahora el movimiento se siente más lento y fluido que en
      la primera prueba (no a saltos). Si sigue pareciendo brusco, prueba a
      bajar la frecuencia de envío (por ejemplo 8-10 Hz en vez de 15) o subir
      los segundos de barrido completo (por ejemplo 12-15s en vez de 8s) la
      próxima vez que lo ejecutes.
- [ ] Pulsa `C` (Center Gimbal): Pan y Tilt deben moverse suavemente (no de
      golpe) hasta sus valores por defecto y pararse ahí.
- [ ] Pulsa `Z` (Reset Zoom): el zoom debe volver suavemente a su valor por
      defecto (normalmente el zoom mínimo, 1x).
- [ ] Mueve el joystick mientras `C` o `Z` están "centrando/reseteando":
      confirma que el movimiento manual cancela el centrado (control
      manual tiene prioridad).
- [ ] **Mientras haces todo esto, OBS sigue mostrando vídeo sin cortes.**

## 6. Si algo falla

Dime, sin que yo asuma nada:

1. Si algún eje no mueve nada (aunque `panIn`/`tiltIn`/`zoomIn` sí cambien
   en pantalla — eso aislaría si es un problema de lectura del joystick o
   de escritura a la cámara).
2. Si el movimiento se siente "a saltos" en vez de suave (puede ser cuestión
   de ajustar la velocidad o el exponente de la curva).
3. Si aparece `ULTIMO ERROR AL ENVIAR A LA CAMARA:` en pantalla, el mensaje
   completo.
4. Qué valores de deadzone/velocidad usaste.

Con eso ajustamos valores por defecto antes de pasar a construir la
interfaz gráfica (Fase 5), que reutilizará exactamente estas mismas clases
de `DjiPtz.Core` (no se reescribe nada del control, solo se le pone una cara
visual con sliders en vez de preguntas de consola).
