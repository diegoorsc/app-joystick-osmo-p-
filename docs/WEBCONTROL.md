# Fase Web — Control remoto desde el celular

Objetivo: mover Pan/Tilt/Zoom de la Pocket 3 desde el navegador de un
celular, sin Raspberry Pi ni gamepad — solo el PC con Windows donde ya
está conectada la cámara (USB-C, modo Webcam).

## Cómo funciona

`DjiPtz.WebControl` es una consola que:

1. Selecciona la cámara igual que `DjiPtz.PtzControl` (Fase 1/4).
2. Levanta un servidor web (Kestrel/ASP.NET Core) en el puerto que elijas
   (por defecto 8080), escuchando en todas las interfaces de red del PC
   (`0.0.0.0`) — por eso es visible desde otros dispositivos de la misma
   red, no solo desde el propio PC.
3. Sirve una única página (`WebUi.cs`, sin dependencias externas — el
   celular no necesita internet, solo estar en la misma WiFi) con:
   - Un **joystick táctil** (arrastrar con el dedo dentro del círculo):
     controla Pan (eje X) / Tilt (eje Y), con la misma deadzone y curva
     progresiva que usaba el gamepad en la Fase 4.
   - Botones **+ / -** (mantener pulsado) para Zoom.
   - Botones **Center Gimbal** / **Reset Zoom**.
   - Interruptores para invertir Pan/Tilt/Zoom si van al revés.
   - Un indicador de conexión y los valores actuales de Pan/Tilt/Zoom.
4. Internamente reutiliza exactamente las mismas piezas que las fases
   anteriores (`AxisDeadzoneCurve` + `PtzAxisController` de
   `DjiPtz.Core`): el navegador solo manda un valor analógico -1..1 por
   eje (igual que mandaría un stick físico), y el PC es quien integra el
   movimiento y limita la frecuencia de envío real a la cámara.
5. Por seguridad, si el celular deja de mandar señal (se cierra la
   pestaña, se corta el WiFi) durante más de 400 ms, el Pan/Tilt/Zoom se
   detienen solos en vez de quedar corriendo con el último valor recibido.

**Nota de seguridad**: la página no pide contraseña — cualquiera conectado
a esa misma red WiFi que sepa la URL puede abrirla y mover la cámara.
Para un evento en un lugar con WiFi propia (no compartida con público) esto
no suele ser un problema; si necesitás restringirlo (por ejemplo un PIN de
acceso), avisá y se agrega.

## 1. Cómo compilar

```powershell
dotnet build DjiPtzController.sln
```

## 2. Cómo ejecutar

1. Conectá la Pocket 3 (modo Webcam) al PC.
2. Conectá el celular a la **misma red WiFi** que el PC (el PC puede estar
   por cable o WiFi, da igual, mientras esté en la misma red/subred que el
   celular).
3. En el PC:

   ```powershell
   dotnet run --project src/DjiPtz.WebControl
   ```

4. Contestá las preguntas (todas tienen un valor por defecto con Enter):
   - Selección de cámara (se autodetecta normalmente).
   - Deadzone del joystick táctil (Enter = 15%).
   - Curva/exponente (Enter = 2.5).
   - Segundos para barrer Pan/Tilt y Zoom a máxima velocidad (Enter = 8s).
   - Frecuencia de envío a la cámara en Hz (Enter = 15).
   - Puerto TCP de la página (Enter = 8080).
5. La consola imprime una o varias direcciones tipo:

   ```
   http://192.168.1.23:8080
   ```

   Abrí esa dirección en el navegador del celular (Chrome/Safari). Si
   Windows Defender Firewall pregunta si permitir el acceso la primera vez
   que corrés el programa, **aceptá** (si no, el celular no va a poder
   conectarse).
6. Movés la cámara arrastrando el joystick en pantalla y usando los
   botones +/- para zoom. Para salir del programa, escribí `Q` y Enter en
   la consola del PC (la cámara queda en la última posición enviada).

## 3. Si algo falla

- **El celular no carga la página / "no se puede acceder al sitio"**:
  confirmá que celular y PC están en la misma red WiFi (no en un WiFi de
  invitados que aísla dispositivos entre sí — muchos routers lo hacen por
  defecto), y que el firewall de Windows no está bloqueando el puerto
  (`Configuración → Privacidad y seguridad → Firewall de Windows → Permitir
  una aplicación`, buscar `DjiPtzWebControl`).
- **No aparece ninguna dirección IP en la consola**: el PC no tiene
  ninguna interfaz de red activa con IPv4 (revisá que el WiFi/Ethernet del
  PC esté realmente conectado a una red, no solo "encendido").
- **El joystick se mueve en pantalla pero la cámara no reacciona**: fijate
  si aparece `ULTIMO ERROR AL ENVIAR A LA CAMARA:` en la consola del PC.
- **Un eje va al revés**: usá los interruptores "Invertir Pan/Tilt/Zoom"
  de la propia página.
