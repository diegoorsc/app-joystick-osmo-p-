# FASE 4 — Mouse + teclado → Pan/Tilt/Zoom

Objetivo: mover Pan/Tilt/Zoom de la Pocket 3 **sin necesitar ningún mando**:
la rueda del mouse mueve el Pan, las flechas Arriba/Abajo del teclado mueven
el Tilt, y las teclas `+`/`-` controlan el Zoom — todo sin cerrar OBS.

No se avanza a la Fase 5 (interfaz gráfica) hasta confirmar que esto se
mueve bien con hardware real.

## Cambio respecto a la versión anterior (que usaba gamepad)

- **Pan → rueda del mouse**: cada "click" de scroll desplaza el Pan una
  cantidad configurable de unidades, y el gimbal se desliza suavemente
  hasta llegar (usa `PtzAxisController.SeekTowards`, la misma primitiva de
  "Center Gimbal"). Girar la rueda hacia un lado más rápido/varias veces
  simplemente encadena más desplazamiento.
- **Tilt → flechas Arriba/Abajo**: mientras se mantienen pulsadas, el Tilt
  se mueve de forma continua (jog), igual que antes hacía el stick
  izquierdo — más tiempo pulsado no cambia la velocidad (es digital, no
  analógico), pero sigue respetando `MaxSpeedUnitsPerSecond`.
- **Zoom → teclas `+`/`-`** (numpad o las de encima de las flechas): mismo
  comportamiento de jog continuo mientras se mantienen pulsadas.
- Ya no hay selección de mando, ni preguntas de deadzone/curva/exponente
  (esos conceptos eran para suavizar un eje analógico; con rueda de mouse y
  teclas digitales no aplican).
- La lectura de rueda y teclado es **global** (no depende de que la consola
  tenga el foco): la rueda se captura con un hook de bajo nivel
  (`WH_MOUSE_LL`) y las flechas/`+`/`-` con `GetAsyncKeyState`, igual que
  antes se leía el estado del mando en cada frame.

## Cómo funciona (arquitectura)

- `DjiPtz.Core/Control/PtzAxisController.cs`: sin cambios. Como la Pocket 3
  solo acepta posiciones **absolutas** por UVC, este controlador sigue
  siendo la pieza clave: integra una velocidad normalizada (`ApplyJogVelocity`,
  usado ahora por Tilt/Zoom) o desliza hacia un valor objetivo
  (`SeekTowards`, usado ahora por Pan y por "Center Gimbal"/"Reset Zoom").
- `DjiPtz.PtzControl/Input/MouseWheelHook.cs`: instala un hook
  `WH_MOUSE_LL` y acumula el delta de rueda (múltiplos de 120) hasta que el
  loop principal lo consume una vez por frame.
- `DjiPtz.PtzControl/Input/NativeKeyboard.cs`: expone el estado actual de
  Arriba/Abajo/`+`/`-` vía `GetAsyncKeyState`, sin pasar por `Console.ReadKey`
  (que solo da eventos de tecla suelta, no "mantenida").
- `DjiPtz.PtzControl/Program.cs`: el programa de esta fase. Junta la
  selección de cámara (Fase 1) con estas dos fuentes de entrada; integra el
  movimiento a ~50 Hz para que sea preciso, pero **limita el envío real de
  comandos a la cámara** a una frecuencia configurable (15 Hz por defecto).
- Las clases de `DjiPtz.Core/Gamepad/` (`GamepadEnumerator`, `RawStickSource`,
  `XInputStickSource`, etc.) **siguen existiendo** porque las usa
  `DjiPtz.GamepadDiagnostics` (Fase 3), pero `DjiPtz.PtzControl` ya no
  depende de ningún mando.

## 1. Cómo compilar

```powershell
dotnet build DjiPtzController.sln
```

## 2. Cómo ejecutar

1. Conecta la Pocket 3 (modo Webcam).
2. (Opcional) Abre OBS con la Pocket 3 capturando, para comprobar en vivo
   que el vídeo no se corta mientras mueves el gimbal.
3. Ejecuta:

   ```powershell
   dotnet run --project src/DjiPtz.PtzControl
   ```

4. Sigue las preguntas:
   - Selecciona la cámara (normalmente se autodetecta).
   - Unidades de Pan por cada "click" de rueda del mouse (Enter = valor
     sugerido según el rango de Pan de tu cámara).
   - Segundos para barrer Pan/Tilt y Zoom a máxima velocidad (Enter = 8s,
     más alto = más lento), y frecuencia de envío a la cámara en Hz
     (Enter = 15; más bajo = más fluido pero menos reactivo).
5. Pulsa una tecla para empezar y usa la rueda del mouse / flechas / `+`/`-`.

## 3. Qué deberías ver

Un panel que se refresca varias veces por segundo:

```
DJI Pocket 3 PTZ Controller - Control por Mouse + Teclado
Camara: DJI Osmo Pocket 3 (o el nombre que reporte Windows)

PAN   min=  -35 max=  215 step=  1  valor=   179  invertido=No
TILT  min=  -90 max=   90 step=  1  valor=     0  invertido=Si
ZOOM  min=  100 max=  400 step=  1  valor=   100  invertido=No

Pan por click de scroll: 2 unidades
Flechas Arriba/Abajo (Tilt): -
Teclas +/- (Zoom): -

Envio a camara: 15 Hz
Velocidad Pan/Tilt: 23.1 u/s (barrido completo en 8.0s)
Velocidad Zoom:     37.5 u/s (barrido completo en 8.0s)

Controles: [Rueda del mouse] Pan  [Flecha Arriba/Abajo] Tilt  [+/-] Zoom
Teclas: [C] Center Gimbal  [Z] Reset Zoom/1x  [1] Invertir Pan  [2] Invertir Tilt  [3] Invertir Zoom  [Q] Salir
```

## 4. Qué comprobar exactamente

- [ ] Sin tocar nada, `valor` de PAN/TILT/ZOOM no cambian (quietos).
- [ ] Gira la rueda del mouse un "click" hacia un lado: el `valor` de PAN
      se desliza suavemente hasta el nuevo objetivo y se detiene ahí (no
      salta de golpe).
- [ ] Gira la rueda varias veces seguidas hacia el mismo lado: PAN sigue
      moviéndose (los desplazamientos se encadenan).
- [ ] El gimbal físico se mueve acorde (compruébalo mirando la cámara, no
      solo los números).
- [ ] Mantén pulsada la flecha Arriba: TILT se mueve progresivamente
      mientras la mantengas (arriba = tilt arriba). Suelta y se detiene.
      Repite con la flecha Abajo.
- [ ] Mantén pulsada `+`: el ZOOM debe acercar progresivamente mientras lo
      mantengas. Mantén pulsada `-`: debe alejar.
- [ ] Confirma en OBS que Pan/Tilt/Zoom cambian de verdad.
- [ ] Si algún eje se mueve **al revés** de lo esperado, pulsa `1`/`2`/`3`
      para invertirlo y confirma que se corrige (para Pan, esto invierte
      el sentido del scroll).
- [ ] Pulsa `C` (Center Gimbal): Pan y Tilt deben moverse suavemente hasta
      sus valores por defecto y pararse ahí.
- [ ] Pulsa `Z` (Reset Zoom): el zoom debe volver suavemente a su valor por
      defecto (normalmente el zoom mínimo, 1x).
- [ ] Gira la rueda o pulsa una flecha mientras `C` o `Z` están
      "centrando/reseteando": confirma que el control manual cancela el
      centrado.
- [ ] Prueba con la consola **sin el foco** (otra ventana encima, p. ej.
      OBS): la rueda y las flechas deberían seguir funcionando igual,
      porque la captura es global.
- [ ] **Mientras haces todo esto, OBS sigue mostrando vídeo sin cortes.**

## 5. Si algo falla

Dime, sin que yo asuma nada:

1. Si algún eje no mueve nada aunque la línea de Pan/Tilt/Zoom en pantalla
   sí cambie (eso aislaría si es un problema de lectura de rueda/teclado o
   de escritura a la cámara).
2. Si aparece un error al arrancar sobre "No se pudo instalar el hook de
   rueda de mouse" (puede pasar si el antivirus bloquea `SetWindowsHookEx`).
3. Si el movimiento se siente "a saltos" en vez de suave (ajusta segundos
   de barrido o Hz de envío, como en la fase anterior).
4. Si aparece `ULTIMO ERROR AL ENVIAR A LA CAMARA:` en pantalla, el mensaje
   completo.

Con eso ajustamos valores por defecto antes de pasar a construir la
interfaz gráfica (Fase 5), que reutilizará exactamente estas mismas clases
de `DjiPtz.Core` (no se reescribe nada del control, solo se le pone una cara
visual con sliders en vez de preguntas de consola).
