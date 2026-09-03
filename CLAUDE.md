# Instrucciones para sesiones de IA — backend

**El documento de entrada del proyecto es `INIT.md`, en el repositorio
`sgcm_script`** (junto a este, en `../sgcm_script/INIT.md` si los tres repos
están clonados en la misma carpeta). Ahí están las reglas, el estado de cada
módulo y los defectos abiertos. Los estándares completos están en
`../sgcm_script/ESTANDARES.md`.

Lo que hay que saber para tocar este repositorio:

- **El backend es un puente.** Resuelve la sesión, llama a un procedimiento y
  devuelve lo que éste responde. La lógica de negocio vive en la base de datos.
  Un controlador que interpreta el JSON del negocio está mal.
- **El bloque `Actor` lo completa el backend desde la sesión**, nunca el
  navegador. Si el cliente pudiera declararlo, podría declararse jefe de otra
  unidad.
- **Un controlador por esquema**, y las acciones del flujo no van aquí: son
  transiciones de estado y se ejecutan por `SigcmController`.
- **La excepción son los correos.** SMTP no corre en SQL Server, así que la
  rutina arma el sobre —destinatario, asunto, cuerpo— y el controlador lo envía
  y vuelve a marcar el resultado. Ese reparto está en `RequerimientoController`
  y en `CmnController.notificarAnexo4`.

## Después de cambiar código: matar el proceso y relevantarlo

`anin_scm` sirve desde `bin/Debug/net8.0/` y **bloquea sus DLL mientras corre**,
así que `dotnet build` falla en el copiado (`MSB3021`) sin que parezca un error
de compilación. El síntoma es un **404 en el endpoint nuevo**, idéntico a una
ruta mal escrita, y se pierde el tiempo revisando el `[HttpGet]`.

```bash
powershell -NoProfile -Command "Stop-Process -Name anin_scm -Force -ErrorAction SilentlyContinue"
```
```bash
dotnet build anin_scm.sln --nologo
```
```bash
dotnet run --project anin.scm --launch-profile https --no-build
```

**Cómo comprobarlo en vez de suponerlo:** un endpoint que existe y pide sesión
responde **401**; sólo una ruta inexistente responde **404**.
