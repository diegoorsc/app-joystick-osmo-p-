# FASE 4 — Joystick → Pan/Tilt/Zoom (deadzone + curva progresiva)

Objetivo: mover Pan/Tilt/Zoom de la Pocket 3 con el mando, con
comportamiento de joystick PTZ profesional (centrado = quieto, desviación
pequeña = lento, desviación grande = rápido), sin cerrar OBS.

No se avanza a la Fase 5 (interfaz gráfica) hasta confirmar que esto se
mueve bien con tu mando real.

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
- `DjiPtz.PtzControl`: el programa de esta fase. Junta la selección de
  cámara (Fase 1) y de mando (Fase 3) y ejecuta un loop de control a ~50 Hz
  que lee el joystick, aplica la curva, mueve Pan/Tilt/Zoom, y solo envía un
  nuevo valor a la cámara cuando realmente cambia (respetando el Step).

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
   - Deadzone (Enter = 15%), curva/exponente (Enter = 2.5), velocidad de
     Pan/Tilt y de Zoom (Enter = usar los valores por defecto).
5. Pulsa una tecla para empezar y mueve los joysticks.

## 4. Qué deberías ver

Un panel que se refresca varias veces por segundo:

```
PAN   min=  -35 max=  215 step=  1  valor=   179  invertido=No
TILT  min=  -90 max=   90 step=  1  valor=     0  invertido=No
ZOOM  min=  100 max=  400 step=  1  valor=   100  invertido=No

Stick izquierdo:  X=  0.02  Y= -0.01   -> panIn= 0.00  tiltIn= 0.00
Stick derecho:    X=  0.00  Y=  0.00   -> zoomIn= 0.00

Deadzone: 15%   Curva (exponente): 2.5
Velocidad Pan/Tilt: 74.0 u/s (barrido completo en 2.5s)
Velocidad Zoom:     75.0 u/s (barrido completo en 4.0s)

Teclas: [C] Center Gimbal  [Z] Reset Zoom/1x  [1] Invertir Pan  [2] Invertir Tilt  [3] Invertir Zoom  [Q] Salir
```

## 5. Qué comprobar exactamente

- [ ] Con el joystick **izquierdo centrado**, `valor` de PAN y TILT no
      cambian (quietos).
- [ ] Desviar el stick izquierdo **un poco** hacia un lado: el `valor` de
      PAN cambia **lentamente**.
- [ ] Desviar el stick izquierdo **al máximo**: PAN cambia **rápido**.
- [ ] El gimbal físico se mueve acorde en cada caso (compruébalo mirando la
      cámara, no solo los números).
- [ ] Repite lo mismo con el eje Y del stick izquierdo para TILT.
- [ ] Repite con el eje Y del stick derecho para ZOOM (¿el zoom físico
      cambia al ver la imagen en OBS?).
- [ ] Si algún eje se mueve **al revés** de lo esperado (por ejemplo, mover
      el stick a la derecha hace PAN hacia la izquierda), pulsa `1`/`2`/`3`
      para invertirlo y confirma que se corrige.
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
