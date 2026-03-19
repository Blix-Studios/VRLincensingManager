# VR Licensing System — Unity UPM Package

[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-black?logo=unity)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-Meta%20Quest-blue)](https://www.meta.com/quest/)

Módulo genérico de licencias y registro de claves para simuladores VR en Meta Quest. Se instala como paquete UPM vía Git y se conecta a **Supabase** para validación de claves.

## Características

- 🔑 **Validación de claves de licencia** contra Supabase REST API
- ⏱️ **Modo Demo** con temporizador configurable (default: 1 hora)
- 🔒 **Almacenamiento encriptado** (AES-256 con clave derivada del dispositivo)
- 🕐 **Anti-manipulación del reloj** (clock rollback detection)
- 📊 **Tracking de tiempo de sesión** con `Time.unscaledTime`
- 🔌 **Auto-inicialización** (`RuntimeInitializeOnLoadMethod`) — Plug & Play
- ⚙️ **Configuración por ScriptableObject** — cada simulador tiene su propio config

## Instalación

### Opción A — Package Manager (recomendada)

```
Window > Package Manager > + > Add package from git URL...
```

Pegar:
```
https://github.com/BlixStudios/com.blixstudios.vr-licensing.git#v1.0.0
```

### Opción B — Editar manifest.json

Agregar a `Packages/manifest.json`:
```json
{
    "dependencies": {
        "com.blixstudios.vr-licensing": "https://github.com/BlixStudios/com.blixstudios.vr-licensing.git#v1.0.0"
    }
}
```

## Configuración Rápida

### 1. Crear el Asset de Configuración

En Unity:
```
Assets > Create > VR Licensing > Nueva Configuracion
```

Esto crea un `LicenseConfig` ScriptableObject. Configurar los campos:

| Campo | Descripción |
|-------|-------------|
| **Supabase Url** | URL de tu proyecto (ej: `https://xxx.supabase.co`) |
| **Anon Key** | Clave pública anon de Supabase |
| **Product Id** | ID del producto/simulador en la tabla `products` |
| **Demo Duration Seconds** | Tiempo de demo en segundos (3600 = 1 hora) |
| **Max Offline Hours** | Horas máximas offline (72 = 3 días) |
| **App Display Name** | Nombre visible del simulador |

### 2. Colocar el Config en Resources

**Mover** el asset `LicenseConfig` a la carpeta `Assets/Resources/` (crearla si no existe). El nombre del archivo **debe** ser `LicenseConfig`.

### 3. (Opcional) Crear Prefab de UI

Crear un prefab llamado `LicenseGateUI` con el componente `LicenseManager` y colocarlo en `Assets/Resources/`. Si no existe, el sistema creará un GameObject vacío con la lógica (sin UI visual).

### 4. ¡Listo!

Al dar Play, el sistema se auto-inicializa antes de cargar cualquier escena.

## Arquitectura

```
LicenseBootstrapper (auto-init)
    └── LicenseManager (orquestador)
        ├── SupabaseClient (HTTP REST API)
        ├── SecureLicenseStorage (AES-256 + PlayerPrefs)
        ├── ClockGuard (anti clock-rollback)
        ├── SessionTimeTracker (Time.unscaledTime)
        └── DemoModeManager (temporizador demo)
```

## Eventos del LicenseManager

Suscríbete a estos eventos desde tu código para reaccionar a cambios:

```csharp
var manager = FindFirstObjectByType<VRLicensing.LicenseManager>();

manager.OnStateChanged += (state) => {
    // LicenseState: Unlicensed, Demo, Licensed, Expired, ClockTampered
    Debug.Log($"Estado: {state}");
};

manager.OnLicenseValidated += (license) => {
    Debug.Log($"Licencia activa: {license.license_type}, expira: {license.expires_at}");
};

manager.OnDemoExpired += () => {
    // Mostrar pantalla de bloqueo
};

manager.OnLicenseExpired += () => {
    // Volver a modo demo o bloquear
};
```

## Enviar una Clave desde la UI

```csharp
manager.SubmitLicenseKey("ABCD-1234-EFGH-5678", (success, error) => {
    if (success)
        Debug.Log("¡Licencia activada!");
    else
        Debug.LogError($"Error: {error}");
});
```

## Estructura de Archivos

```
Runtime/
├── Scripts/
│   ├── LicenseManager.cs          # Orquestador principal
│   ├── LicenseBootstrapper.cs     # Auto-init BeforeSceneLoad
│   ├── LicenseConfig.cs           # ScriptableObject de configuración
│   ├── LicenseData.cs             # Modelo de datos (mapea user_licenses)
│   ├── SupabaseClient.cs          # HTTP client para Supabase REST API
│   ├── SecureLicenseStorage.cs    # Almacenamiento AES-256
│   ├── ClockGuard.cs              # Detección de clock rollback
│   ├── SessionTimeTracker.cs      # Contador de tiempo real
│   └── DemoModeManager.cs         # Temporizador de demo
├── Prefabs/                       # Prefabs de UI (por crear)
└── VRLicensing.Runtime.asmdef
Editor/
└── VRLicensing.Editor.asmdef
```

## Requisitos

- **Unity** 6000.0+ (probado en 6000.3.10f1)
- **Meta XR SDK** (opcional, para obtener Oculus User ID)
- **Supabase** proyecto con tabla `user_licenses`
- **IL2CPP** scripting backend (estándar para Quest builds)

## Schema Supabase Requerido

Tu tabla `user_licenses` debe tener al menos:

```sql
CREATE TABLE user_licenses (
    id uuid DEFAULT gen_random_uuid() PRIMARY KEY,
    license_key text NOT NULL,
    license_type text NOT NULL,     -- 'weekly', 'monthly', 'annual'
    status text DEFAULT 'active',    -- 'active', 'expired', 'cancelled', 'suspended'
    product_id integer NOT NULL,
    user_id uuid NOT NULL,
    order_id uuid,
    starts_at timestamptz DEFAULT now(),
    expires_at timestamptz NOT NULL,
    created_at timestamptz DEFAULT now(),
    updated_at timestamptz DEFAULT now()
);
```

## Licencia

Propiedad de Blix Studios. Uso exclusivo para proyectos autorizados.
