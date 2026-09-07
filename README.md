# ManWin Performance Overlay

ManWin es una aplicacion para Windows inspirada en MangoHud. Usa RTSS y
OverlayEditor para mostrar un overlay de rendimiento configurable dentro de
juegos y aplicaciones compatibles.

## Funciones

- Overlay horizontal de una sola linea, estilo MangoHud.
- FPS, frametime, uso y temperatura de CPU/GPU.
- Uso de RAM y VRAM.
- Frecuencias, consumo, porcentajes de memoria y grafica de frametime.
- Separadores visuales y reacomodo automatico cuando una metrica se desactiva.
- Perfiles por ejecutable detectado por RTSS.
- Interfaz disponible en espanol e ingles.
- Aplicacion con privilegios de administrador para facilitar la integracion con RTSS.

## Requisitos

- Windows 10 u 11 de 64 bits.
- [RivaTuner Statistics Server (RTSS)](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/).
- RTSS con el plugin `OverlayEditor.dll` activo.
- .NET 9 Desktop Runtime x86 para ejecutar el build incluido.
- Microsoft Edge WebView2 Runtime.
- Al menos un proveedor de sensores configurado en RTSS, por ejemplo Internal HAL
  o LibreHardwareMonitor.

## Configuracion de RTSS

1. Abre RTSS.
2. Confirma que **On-Screen Display support** este activado.
3. Abre **Setup > Plugins**.
4. Activa `OverlayEditor.dll`.
5. Deja RTSS ejecutandose antes de abrir ManWin y el juego.

## Ejecutar la aplicacion

Conserva toda la carpeta `release` y ejecuta `release/ManWin.exe` como
administrador. No muevas el ejecutable fuera de esa carpeta: necesita `Web/`,
`Overlays/`, `runtimes/` y las dependencias de WebView2 que estan junto a el.

La aplicacion se conecta a RTSS mediante `RTSSSharedMemoryV2`, copia el layout
de ManWin a la carpeta de OverlayEditor y aplica los cambios cuando se cambia
una metrica.

## Uso

1. Inicia RTSS y verifica el plugin OverlayEditor.
2. Abre ManWin.
3. En **Diseño OSD**, activa o desactiva las metricas deseadas.
4. Abre el juego. Si ya estaba abierto, reinicialo para que RTSS recargue el layout.
5. Usa **Perfiles** para guardar preferencias por ejecutable.
6. Cambia el idioma desde el selector de la barra lateral.

## Contenido del build

```text
release/
  ManWin.exe                  Aplicacion
  Web/                        Interfaz e idiomas
  Overlays/                   Layout de OverlayEditor
  runtimes/                   Loader nativo de WebView2
```

## Compilar desde el codigo fuente

```powershell
dotnet build src/MangoHudWindows/MangoHudWindows.csproj
```

El codigo fuente no se incluye en esta rama de distribucion. El proyecto genera
una aplicacion x86 para mantener compatibilidad con la interfaz de memoria
compartida de RTSS.

## Limitaciones actuales

- Los valores de hardware dependen de los proveedores configurados en RTSS.
- La escritura de limites FPS por perfil aun esta preparada para una siguiente
  etapa de integracion.
- OverlayEditor debe permanecer activo en RTSS para mostrar el layout.

## Licencia

Proyecto en desarrollo. Agrega aqui la licencia que quieras utilizar antes de
publicar una version distribuible.
