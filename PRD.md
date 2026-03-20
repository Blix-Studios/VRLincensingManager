# Product Requirements Document (PRD) — Sistema de Licencias VR

**Proyecto:** `com.blixstudios.vr-licensing` (VR Licensing Manager)
**Autor:** Blix Studios
**Descripción:** Paquete UPM autónomo que provee un sistema completo de validación, gestión y seguridad de licencias para simuladores en Realidad Virtual (Unity).

---

## 📌 Índice
1. [Visión General](#1-visión-general)
2. [Flujo del Sistema y Estados](#2-flujo-del-sistema-y-estados)
3. [Arquitectura del Proyecto (Scripts)](#3-arquitectura-del-proyecto-scripts)
4. [Dependencias y Reflexión](#4-dependencias-y-reflexión)
5. [Consideraciones de Seguridad](#5-consideraciones-de-seguridad)

---

## 1. Visión General
El objetivo de este sistema es proteger las aplicaciones VR de uso no autorizado, ofreciendo un **modo de prueba gratuito (Demo)** por tiempo limitado (ej. 1 hora) y un flujo de activación mediante **Claves de Licencia** conectadas a una plataforma Web basada en **Supabase**.

El paquete está diseñado para ser "plug-and-play" en cualquier proyecto Unity (6000.0+), generando su propia Interfaz de Usuario (UI) en tiempo de ejecución sin depender de Prefabs externos, para evitar problemas de referencias GUID y dependencias incompatibles en el XR Interaction Toolkit.

---

## 2. Flujo del Sistema y Estados

### Estados (`LicenseState`)
1. **Unlicensed (Welcome):** El usuario no tiene licencia y el demo no ha iniciado. Se muestran 3 opciones:
   - Iniciar Demo Gratuita
   - Ingresar Clave de Licencia
   - Escanear QR (Activa cámara passthrough)
2. **Demo:** El usuario está jugando el demo. La UI se oculta. El tiempo se trackea en tiempo real.
3. **Licensed:** El simulador está desbloqueado completamente usando una clave validada.
4. **Expired:** El tiempo del demo o la licencia han expirado. La UI reaparece solicitando una clave forzosamente.
5. **ClockTampered:** Se detectó que el usuario alteró el reloj interno de su PC/Gafas VR para engañar al sistema. Bloquea el acceso hasta verificar con un servidor de tiempo NTP.

---

## 3. Arquitectura del Proyecto (Scripts)

Ubicación principal: `Runtime/Scripts/`

### 🏗️ Core & Orchestration
- **`LicenseManager.cs`**: Orquestador principal. Máquina de estados. Controla el flujo entre empezar demos, someter claves de licencia, y notificar a la UI los cambios de estado correspondientes.
- **`LicenseBootstrapper.cs`**: Script automático (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`). Carga la configuración (`LicenseConfig`) y levanta el `LicenseManager` y la UI apenas el proyecto arranca.
- **`LicenseConfig.cs`**: `ScriptableObject` donde el desarrollador introduce su `Supabase URL`, `PlayFab ID`/`Product ID`, tiempo de demo y nombre de la App.

### 🎨 Interfaz de Usuario (UI)
- **`LicenseUIBuilder.cs`**: Script gigantesco (Factory) responsable de generar los paneles de UI desde cero utilizando código. 
  - Genera un `Canvas WorldSpace` inmersivo.
  - Implementa el **Welcome Panel, Key Input Panel y Demo Expired Panel**.
  - Asigna `TrackedDeviceGraphicRaycaster` y usa `LazyFollow` (persigue tu cabeza suavemente) vía Reflexión.
  - Gestiona la visualización del teclado y la lógica de "auto-salto" de los campos de input (ej. `XXXX` -> `XXXX`).

### 🔒 Backend & Supabase
- **`SupabaseClient.cs`**: Cliente REST HTTP ligero para conectarse a Supabase de Blix Studios.
  - Ejecuta la llamada a la Edge Function (`/functions/v1/redeem-license`) para validar si la clave existe y consumirla.
  - Soporta caché de JWT local para validaciones offline (hasta `maxOfflineHours`).

### ⏳ Manejo de Tiempo (Demo Mode)
- **`DemoModeManager.cs`**: Trackea el progreso en tiempo real durante la sesión de juego. Al expirar dispara los eventos para bloquear el juego.
- **`SessionTimeTracker.cs`**: Funciones auxiliares genéricas para calcular el paso real de frames y sesiones.

### 🛡️ Seguridad y Anti-Piratería
- **`SecureLicenseStorage.cs`**: Gestiona cómo se guardan localmente el JWT de la licencia y los segundos de demo utilizados. 
  - (En su diseño ideal utiliza encripción simétrica AES o el `ProtectedData` en Windows para evitar manipulación de local files).
- **`ClockGuard.cs`**: Sistema anti-rollback de reloj. Consulta a servidores NTP (como `pool.ntp.org`) por UDP y guarda la fecha más alta detectada. Si el tiempo actual del sistema local es "menor" al guardado, dispara el `LicenseState.ClockTampered`.

---

## 4. Dependencias y Reflexión

Para mantener el SDK agnóstico y evitar errores de compilación, el `asmdef` (`VRLicensing.Runtime.asmdef`) **no depende forzosamente de XR Interaction Toolkit o Oculus SDK**. Accede a ellos vía **Reflexión C# (`Type.GetType`)**:
- **XR Interaction Toolkit (XRI)**:
  - Extrae `TrackedDeviceGraphicRaycaster` para interactuar con rayos láser.
  - Extrae `LazyFollow` rellenando sus campos anidados (`m_TargetConfig`, `m_PositionFollowParams`) para suavizado de cámara.
- **Meta XR SDK**:
  - Extrae `OVRManager` para poder habilitar el Passthrough (`isInsightPassthroughEnabled = true`), permitiendo escanear QR's levantándote las gafas.

---

## 5. Consideraciones para el Agente de IA

Si el usuario (USER) reabre un contexto en el futuro pidiendo modificaciones aquí:
1. **UI Modifications**: Deben hacerse dentro de `LicenseUIBuilder.cs`. Todo es generado por código. Cuida la sintaxis UGUI (`RectTransform`, `HorizontalLayoutGroup`). Revisa las líneas de "colors and text alignment".
2. **Web / Supabase Integration**: Las funciones de canje residen en `SupabaseClient.cs`. Las Edge Functions del lado del server validan y queman el cupón, retornando un JWT que es consumido.
3. **Offline mode**: Se permite jugar offline sí la última validación local del JWT expira antes del check del servidor. Ajustable en `LicenseConfig`.
