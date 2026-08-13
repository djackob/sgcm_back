-- DROP FUNCTION public.fn_documento_listar(json);

CREATE OR REPLACE FUNCTION public.fn_documento_listar(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

BEGIN
    RETURN (
        WITH tmpData AS
        (
            SELECT
                doc.iddocumento,
                doc.idtipodocumento,
                doc.nomdocumento
            FROM public.tm_documento doc
            WHERE doc.activo = true
        )
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (SELECT count(*) FROM tmpData) AS cantidad,
                (
                    SELECT COALESCE(array_to_json(array_agg(tmpDat)), '[]'::json)
                    FROM (
                        SELECT
                            iddocumento,
                            idtipodocumento,
                            nomdocumento
                        FROM tmpData
                        ORDER BY nomdocumento
                    ) tmpDat
                ) AS data
        ) resultado
    );

END;
$function$
;

-- DROP FUNCTION public.fn_documento_traeruno(json);

CREATE OR REPLACE FUNCTION public.fn_documento_traeruno(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _iddocumento integer;
BEGIN
    _iddocumento := (_parametro->>'iddocumento')::integer;

    RETURN (
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                doc.iddocumento,
                doc.idtipodocumento,
                doc.nomdocumento,
                doc.activo,
                COALESCE(
                    (
                        SELECT array_to_json(array_agg(sec))
                        FROM (
                            SELECT
                                d.iddocumento_seccion,
                                d.idseccion,
                                s.nomseccion,
                                d.nro_orden,
                                d.texto_seccion,
                                d.texto2_seccion,
                                d.texto3_seccion,
                                d.texto4_seccion,
                                d.indupdate,
                                d.indelemento,
                                d.indentregable,
                                d.activo
                            FROM public.td_documento_seccion d
                            INNER JOIN public.tm_seccion s ON d.idseccion = s.idseccion
                            WHERE d.iddocumento = doc.iddocumento
                              AND d.activo = true
                            ORDER BY d.nro_orden
                        ) sec
                    ), '[]'::json
                ) AS detalle
            FROM public.tm_documento doc
            WHERE doc.iddocumento = _iddocumento
        ) resultado
    );

END;
$function$
;

-- DROP FUNCTION public.fn_insertar_td_operaciones_auditoria(json);

CREATE OR REPLACE FUNCTION public.fn_insertar_td_operaciones_auditoria(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
	_fechaActual timestamp without time zone;
	_id_auditoria integer;

BEGIN
	_fechaActual := now();

	INSERT INTO public.td_operaciones_auditoria
	(
		es_correcto,
		accion,
		nombre_tabla,
		funcion,
		parametro_entrada,
		salida,
		ip,
		usuario,
		fecha
	)
	VALUES
	(
		(_parametro->>'es_correcto')::bool,
		_parametro->>'accion',
		_parametro->>'nombre_tabla',
		_parametro->>'funcion',
		(_parametro->'parametro_entrada')::jsonb,
		(_parametro->'salida')::jsonb,
		_parametro->>'ip',
		_parametro->>'usuario',
		_fechaActual
	)
	RETURNING id_auditoria INTO _id_auditoria;

	RETURN json_build_object('id_auditoria', _id_auditoria);

END;
$function$
;

-- DROP FUNCTION public.fn_listar_tipo_documento();

CREATE OR REPLACE FUNCTION public.fn_listar_tipo_documento()
 RETURNS json
 LANGUAGE plpgsql
AS $function$
BEGIN
    RETURN (
        SELECT json_build_object(
            'cantidad', COUNT(*),
            'data', COALESCE(
                json_agg(
                    json_build_object(
                        'idtipodoc',      td.idtipodoc,
                        'nombre_tipodoc', td.nombre_tipodoc
                    )
                    ORDER BY td.idtipodoc ASC
                ), '[]'::json
            )
        )
        FROM public.tm_tipodocumento td
    );

EXCEPTION WHEN OTHERS THEN
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_movimiento_listar(json);

CREATE OR REPLACE FUNCTION public.fn_movimiento_listar(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _idoperacion bigint;
BEGIN
    _idoperacion := (_parametro->>'idoperacion')::bigint;

    RETURN (
        WITH tmpData AS
        (
            SELECT
                mov.fecregtrx AS fecha,
                t.nomtransaccion,
                mov.nomusuario,
                mov.idusuario,
                mov.texto_trx
            FROM public.td_movimiento mov
            INNER JOIN public.tm_transaccion t ON mov.idtrx = t.idtrx
            WHERE mov.idoperacion = _idoperacion
        )
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (SELECT count(*) FROM tmpData) AS cantidad,
                (
                    SELECT COALESCE(array_to_json(array_agg(tmpDat)), '[]'::json)
                    FROM (
                        SELECT
                            fecha,
                            nomtransaccion,
                            nomusuario,
                            idusuario,
                            texto_trx
                        FROM tmpData
                        ORDER BY fecha DESC
                    ) tmpDat
                ) AS data
        ) resultado
    );

END;
$function$
;

-- DROP FUNCTION public.fn_operacion_actualizar_ruta_upload(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_actualizar_ruta_upload(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _idoperacion bigint;
    _mensaje     json;
    _error       text;
BEGIN
    BEGIN
        _idoperacion := (_parametro->>'idoperacion')::bigint;

        UPDATE public.tm_operacion
        SET
            ruta_documento       = _parametro->>'nomdocumento',
            usuario_modificacion = (_parametro->>'idusuario')::int4,
            fecha_modificacion   = now()
        WHERE idoperacion = _idoperacion;

        _mensaje := json_build_object(
            'estado', 1,
            'idoperacion', _idoperacion,
            'mensaje', 'Se actualizó la ruta del documento satisfactoriamente'
        );

        RETURN _mensaje;

    EXCEPTION
        WHEN OTHERS THEN
            GET STACKED DIAGNOSTICS _error = MESSAGE_TEXT;

            RETURN json_build_object(
                'estado', 0,
                'idoperacion', 0,
                'mensaje', 'No se pudo realizar el proceso'
            );
    END;

END;
$function$
;

-- DROP FUNCTION public.fn_operacion_actualizar_ruta_upload_firma(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_actualizar_ruta_upload_firma(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _idoperacion bigint;
    _mensaje     json;
    _error       text;
BEGIN
    BEGIN
        _idoperacion := (_parametro->>'idoperacion')::bigint;

        UPDATE public.tm_operacion
        SET
            ruta_documento_firma = _parametro->>'nomdocumento',
            usuario_modificacion = (_parametro->>'idusuario')::int4,
            fecha_modificacion   = now()
        WHERE idoperacion = _idoperacion;

        _mensaje := json_build_object(
            'estado', 1,
            'idoperacion', _idoperacion,
            'mensaje', 'Se actualizó la ruta del documento firmado satisfactoriamente'
        );

        RETURN _mensaje;

    EXCEPTION
        WHEN OTHERS THEN
            GET STACKED DIAGNOSTICS _error = MESSAGE_TEXT;

            RETURN json_build_object(
                'estado', 0,
                'idoperacion', 0,
                'mensaje', 'No se pudo realizar el proceso'
            );
    END;

END;
$function$
;

-- DROP FUNCTION public.fn_operacion_asset_listar(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_asset_listar(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _idoperacion  bigint;
    _idelemento   bigint;
    _identregable bigint;
    _idfase       int4;
    _idtrack      int4;
BEGIN
    _idoperacion  := (_parametro->>'idoperacion')::bigint;
    _idelemento   := COALESCE((_parametro->>'idelemento')::bigint, 0);
    _identregable := COALESCE((_parametro->>'identregable')::bigint, 0);
    _idfase       := COALESCE((_parametro->>'idfase')::int4, 0);
    _idtrack      := COALESCE((_parametro->>'idtrack')::int4, 0);

    RETURN (
        WITH tmpData AS
        (
            SELECT
                a.idasset,
                a.idoperacion,
                a.url_servicio,
                a.cod_file,
                a.idelemento,
                a.identregable,
                a.idfase,
                a.idtrack,
                a.idestado,
                a.documento_sistema
            FROM public.td_operacion_asset a
            WHERE
                a.activo = true
                AND a.idoperacion = _idoperacion
                AND (_idelemento   = 0 OR a.idelemento   = _idelemento)
                AND (_identregable = 0 OR a.identregable = _identregable)
                AND (_idfase       = 0 OR a.idfase       = _idfase)
                AND (_idtrack      = 0 OR a.idtrack      = _idtrack)
        )
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (SELECT count(*) FROM tmpData) AS cantidad,
                (
                    SELECT COALESCE(array_to_json(array_agg(tmpDat)), '[]'::json)
                    FROM (
                        SELECT
                            idasset,
                            idoperacion,
                            url_servicio,
                            cod_file,
                            idelemento,
                            identregable,
                            idfase,
                            idtrack,
                            idestado,
                            documento_sistema
                        FROM tmpData
                        ORDER BY idasset DESC
                    ) tmpDat
                ) AS data
        ) resultado
    );

END;
$function$
;

-- DROP FUNCTION public.fn_operacion_asset_listar_expediente(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_asset_listar_expediente(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _idoperacion bigint;
    _idusuario   int4;
BEGIN
    _idoperacion := (_parametro->>'idoperacion')::bigint;
    _idusuario   := (_parametro->>'idusuario')::int4;

    RETURN (
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (SELECT COUNT(*) FROM public.td_operacion_asset
                 WHERE idoperacion = _idoperacion) AS cantidad,
                COALESCE(
                    (
                        SELECT array_to_json(array_agg(asset))
                        FROM (
                            SELECT
                                A.idasset,
                                A.idtrack,
                                A.fecha_creacion       AS fecharegistro,
                                A.nomasset,
                                A.usuario_creacion     AS idusuario,
                                A.nomusuario           AS nomusuario,
                                A.dependencia_usuario  AS dependencia_usuario,
                                A.documento_sistema,
                                A.idoperacion,
                                B.nomtrack             AS nombretrack,
                                CASE WHEN A.idtipoasset = 999
                                     THEN A.nombre_otros_asset
                                     ELSE C.nombre_tipodoc
                                END                    AS nombre_tipodoc
                            FROM public.td_operacion_asset A
                            LEFT JOIN public.tm_trackflow    B ON A.idtrack    = B.idtrack
                            LEFT JOIN public.tm_tipodocumento C ON A.idtipoasset = C.idtipodoc
                            WHERE A.idoperacion = _idoperacion
                            ORDER BY A.fecha_creacion DESC
                        ) asset
                    ), '[]'::json
                ) AS data
        ) resultado
    );

EXCEPTION WHEN OTHERS THEN
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_operacion_checklist_actualizar_estado(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_checklist_actualizar_estado(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _idusuario        int4;
    _item             json;
    _idoperacion      bigint;
    _idtrack          int4;
    _orden            int4;
    _estado_checklist int4;
    _mensaje          json;
BEGIN
    _idusuario := (_parametro->>'idusuario')::int4;

    FOR _item IN SELECT * FROM json_array_elements(_parametro->'items')
    LOOP
        _idoperacion      := (_item->>'idoperacion')::bigint;
        _idtrack          := (_item->>'idtrack')::int4;
        _orden            := (_item->>'orden')::int4;
        _estado_checklist := (_item->>'estado_checklist')::int4;

        UPDATE public.td_operacion_checklist
        SET estado_checklist = _estado_checklist
        WHERE idoperacion = _idoperacion
          AND idtrack     = _idtrack
          AND orden       = _orden;
    END LOOP;

    _mensaje := json_build_object(
        'estado', 1,
        'mensaje', 'Estados de checklist actualizados correctamente'
    );

    PERFORM public.fn_insertar_td_operaciones_auditoria(
        json_build_object(
            'es_correcto', true,
            'accion', 'ActualizarEstadoChecklist',
            'nombre_tabla', 'public.td_operacion_checklist',
            'funcion', 'public.fn_operacion_checklist_actualizar_estado',
            'parametro_entrada', _parametro,
            'salida', _mensaje,
            'ip', null,
            'usuario', _idusuario::varchar
        )
    );

    RETURN _mensaje;

EXCEPTION WHEN OTHERS THEN
    PERFORM public.fn_insertar_td_operaciones_auditoria(
        json_build_object(
            'es_correcto', false,
            'accion', 'ActualizarEstadoChecklist',
            'nombre_tabla', 'public.td_operacion_checklist',
            'funcion', 'public.fn_operacion_checklist_actualizar_estado',
            'parametro_entrada', _parametro,
            'salida', json_build_object('estado', 0, 'mensaje', SQLERRM),
            'ip', null,
            'usuario', _idusuario::varchar
        )
    );
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_operacion_checklist_listar(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_checklist_listar(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _idoperacion bigint;
    _idtrack     int4;
BEGIN
    _idoperacion := (_parametro->>'idoperacion')::bigint;
    _idtrack     := (_parametro->>'idtrack')::int4;

    RETURN (
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (SELECT COUNT(*) FROM public.td_operacion_checklist
                 WHERE idoperacion = _idoperacion
                   AND idtrack     = _idtrack) AS cantidad,
                COALESCE(
                    (
                        SELECT array_to_json(array_agg(ch ORDER BY ch.orden))
                        FROM (
                            SELECT
                                c.idoperacion,
                                c.idtrack,
                                c.orden,
                                c.nombre_checklist,
                                c.idtipodoc,
                                c.documento_sistema,
                                c.estado_checklist
                            FROM public.td_operacion_checklist c
                            WHERE c.idoperacion = _idoperacion
                              AND c.idtrack     = _idtrack
                        ) ch
                    ), '[]'::json
                ) AS data
        ) resultado
    );

EXCEPTION WHEN OTHERS THEN
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_operacion_clonar_como_tdr(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_clonar_como_tdr(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _idoperacion        bigint;
    _idusuario          int;
    _fechaActual        timestamp := now();

    -- Cabecera origen
    _cab                tm_operacion%ROWTYPE;

    -- Nueva operacion
    _idoperacion_nueva  bigint;
    _idoperaciondet_new bigint;

    -- Filas de template, pedidos y movimientos
    _sec                td_documento_seccion%ROWTYPE;
    _ped                tm_operacion_pedido%ROWTYPE;
    _mov                td_movimiento%ROWTYPE;

BEGIN
    _idoperacion := (_parametro->>'idoperacion')::bigint;
    _idusuario   := (_parametro->>'idusuario')::int;

    -- Leer cabecera origen
    SELECT * INTO _cab
    FROM tm_operacion
    WHERE idoperacion = _idoperacion AND activo = true;

    IF NOT FOUND THEN
        PERFORM public.fn_insertar_td_operaciones_auditoria(
            json_build_object(
                'es_correcto', false,
                'accion', 'CLONAR_TDR',
                'nombre_tabla', 'public.tm_operacion',
                'funcion', 'public.fn_operacion_clonar_como_tdr',
                'parametro_entrada', _parametro,
                'salida', json_build_object('estado', 0, 'mensaje', 'Operacion origen no encontrada'),
                'ip', null,
                'usuario', _idusuario::varchar
            )
        );
        RETURN json_build_object('estado', 0, 'mensaje', 'Operacion origen no encontrada');
    END IF;

    -- --------------------------------------------------------
    -- 1. Insertar nueva cabecera con iddocumento=3
    -- --------------------------------------------------------
    INSERT INTO tm_operacion (
        iddocumento, usuario_creacion, fecoperacion,
        fecha_creacion, activo,
        centro_costo, cod_uo, nombre_depend, abreviado_depend,
        ano_eje,
        denominacion_contratacion, idestado, plazo,
        idtrack, idproveedor,
        cantidad_entregables, indproyecto, nombre_proyecto,
        monto_mensual, monto_total, idperfil
    )
    VALUES (
        3, _cab.usuario_creacion, CURRENT_DATE,
        _fechaActual, true,
        _cab.centro_costo, _cab.cod_uo, _cab.nombre_depend, _cab.abreviado_depend,
        _cab.ano_eje,
        _cab.denominacion_contratacion, 1, _cab.plazo,
        case when _cab.idperfil = 15 then 110 when _cab.idperfil=14 then 210 else 10 end, _cab.idproveedor,
        _cab.cantidad_entregables, _cab.indproyecto, _cab.nombre_proyecto,
        _cab.monto_mensual, _cab.monto_total, _cab.idperfil
    )
    RETURNING idoperacion INTO _idoperacion_nueva;

    -- --------------------------------------------------------
    -- 2. Crear detalle desde plantilla td_documento_seccion
    --    donde iddocumento=3 (Anexo 3 TDR)
    -- --------------------------------------------------------
    FOR _sec IN
        SELECT * FROM td_documento_seccion
        WHERE iddocumento = 3 AND activo = true
        ORDER BY nro_orden
    LOOP
        INSERT INTO td_operacion_detalle (
            idoperacion, idseccion, nro_orden,
            texto_seccion, texto2_seccion, texto3_seccion, texto4_seccion,
            indupdate, activo,
            usuario_creacion, fecha_creacion
        )
        VALUES (
            _idoperacion_nueva, _sec.idseccion, _sec.nro_orden,
            _sec.texto_seccion, _sec.texto2_seccion, _sec.texto3_seccion, _sec.texto4_seccion,
            _sec.indupdate, true,
            _idusuario, _fechaActual
        );
    END LOOP;

    -- --------------------------------------------------------
    -- 3. Copiar pedidos desde la operacion origen
    -- --------------------------------------------------------
    FOR _ped IN
        SELECT * FROM tm_operacion_pedido
        WHERE idoperacion = _idoperacion AND activo = true
    LOOP
        INSERT INTO tm_operacion_pedido (
            idoperacion, usuario_creacion, fecha_creacion, activo,
            ano_eje, nro_pedido, actividad_operativa, meta_presupuestaria,
            ff_rb, programa, prod_py, clasificador,
            cod_item_pedido, nom_item_pedido
        )
        VALUES (
            _idoperacion_nueva, _idusuario, _fechaActual, true,
            _ped.ano_eje, _ped.nro_pedido, _ped.actividad_operativa, _ped.meta_presupuestaria,
            _ped.ff_rb, _ped.programa, _ped.prod_py, _ped.clasificador,
            _ped.cod_item_pedido, _ped.nom_item_pedido
        );
    END LOOP;

    -- --------------------------------------------------------
    -- 4. Copiar movimientos desde la operacion origen
    -- --------------------------------------------------------
    FOR _mov IN
        SELECT * FROM td_movimiento
        WHERE idoperacion = _idoperacion
        ORDER BY idmov
    LOOP
        INSERT INTO td_movimiento (
            idtrx, fectrx, idusuario, fecregtrx,
            idperfil, nomusuario, codperfil,
            idoperacion, texto_trx, idtrx_destino,
            idusuario_asignado, nomusuario_asignado, cod_perfil,
            idtrack_destino
        )
        VALUES (
            _mov.idtrx, _mov.fectrx, _mov.idusuario, _mov.fecregtrx,
            _mov.idperfil, _mov.nomusuario, _mov.codperfil,
            _idoperacion_nueva, _mov.texto_trx, _mov.idtrx_destino,
            _mov.idusuario_asignado, _mov.nomusuario_asignado, _mov.cod_perfil,
            _mov.idtrack_destino
        );
    END LOOP;

    PERFORM public.fn_insertar_td_operaciones_auditoria(
        json_build_object(
            'es_correcto', true,
            'accion', 'CLONAR_TDR',
            'nombre_tabla', 'public.tm_operacion',
            'funcion', 'public.fn_operacion_clonar_como_tdr',
            'parametro_entrada', _parametro,
            'salida', json_build_object('estado', 1, 'idoperacion', _idoperacion_nueva),
            'ip', null,
            'usuario', _idusuario::varchar
        )
    );

    RETURN json_build_object(
        'estado', 1,
        'mensaje', 'Operacion TDR creada correctamente',
        'idoperacion', _idoperacion_nueva
    );

EXCEPTION WHEN OTHERS THEN
    PERFORM public.fn_insertar_td_operaciones_auditoria(
        json_build_object(
            'es_correcto', false,
            'accion', 'CLONAR_TDR',
            'nombre_tabla', 'public.tm_operacion',
            'funcion', 'public.fn_operacion_clonar_como_tdr',
            'parametro_entrada', _parametro,
            'salida', json_build_object('estado', 0, 'mensaje', SQLERRM),
            'ip', null,
            'usuario', _idusuario::varchar
        )
    );
    RETURN json_build_object('estado', 0, 'mensaje', 'No se pudo realizar el proceso');
END;
$function$
;

-- DROP FUNCTION public.fn_operacion_ejecutartrx(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_ejecutartrx(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _idoperacion   bigint;
    _idtrx         int4;
    _idusuario     int4;
    _nomusuario    varchar(80);
    _idperfil      int4;
    _texto_trx     text;
    _idtrackflow         int4;
    _idestado            int4;
    _mensaje             json;
    _error               text;
    _accion              varchar(100) := 'EjecutarTrx';
    _iddocumento         int4;
    _idusuario_asignado  int4;
    _nomusuario_asignado varchar(80);
    _cod_perfil          varchar;
	_fechaActual       	 timestamp without time zone;
BEGIN
    BEGIN
		_fechaActual 		 := now();
        _idoperacion         := (_parametro->>'idoperacion')::bigint;
        _idtrx               := (_parametro->>'idtrx')::int4;
        _idusuario           := (_parametro->>'idusuario')::int4;
        _nomusuario          := _parametro->>'nomusuario';
        _idperfil            := (_parametro->>'idperfil')::int4;
        _texto_trx           := _parametro->>'texto_trx';
        _idusuario_asignado  := (_parametro->>'idusuario_asignado')::int4;
        _nomusuario_asignado := _parametro->>'nomusuario_asignado';
        _cod_perfil          := _parametro->>'cod_perfil';

        SELECT iddocumento INTO _iddocumento
        FROM public.tm_operacion
        WHERE idoperacion = _idoperacion;

        -- Validaciones previas para idtrx = 133 o 112 (solo aplica para iddocumento = 3)
        IF (_idtrx = 133 ) AND (_iddocumento = 3) THEN --OR _idtrx = 112
            IF NOT EXISTS (
                --SELECT 1 FROM public.td_operacion_detalle
                --WHERE idoperacion = _idoperacion AND activo = true
				select 1 from td_operacion_detalle A
				join td_elemento B on A.idoperaciondet  = B.idoperaciondet 
				where A.idoperacion = _idoperacion AND B.activo = true

            ) THEN
                RETURN json_build_object('estado', 0, 'mensaje', 'El Requerimiento no tiene Actividades');
            END IF;

            /*IF NOT EXISTS (
                SELECT 1 FROM public.tm_operacion_esquela
                WHERE idoperacion = _idoperacion AND activo = true
            ) THEN
                RETURN json_build_object('estado', 0, 'mensaje', 'No ha registrado su Solicitud');
            END IF;*/
        END IF;

		/*IF (_idtrx = 109 ) AND (_iddocumento = 3) THEN --OR _idtrx = 112
            IF NOT EXISTS (
                SELECT 1 FROM public.tm_operacion_esquela
                WHERE idoperacion = _idoperacion AND activo = true
            ) THEN
                RETURN json_build_object('estado', 0, 'mensaje', 'No ha registrado su Solicitud');
            END IF;
        END IF;*/


        SELECT idestado INTO _idestado
        FROM public.tm_transaccion
        WHERE idtrx = _idtrx;

        SELECT rpt.idtrack_destino INTO _idtrackflow
        FROM public.tm_rel_perfil_trackflow rpt
        WHERE rpt.idperfil = _idperfil
          AND rpt.idtrx    = _idtrx
          AND rpt.idtrack  = (SELECT idtrack FROM public.tm_operacion WHERE idoperacion = _idoperacion);

        UPDATE public.tm_operacion
        SET
            idtrack            = _idtrackflow,
            idestado           = CASE WHEN _idestado = 0 THEN idestado ELSE _idestado END,
            idusuario_asignado = CASE WHEN _idusuario_asignado IS NOT NULL AND _idusuario_asignado <> 0 THEN _idusuario_asignado ELSE idusuario_asignado END
        WHERE idoperacion = _idoperacion;

        INSERT INTO public.td_movimiento(
            idtrx, fectrx, idusuario, fecregtrx,
            idperfil, nomusuario, codperfil,
            idoperacion, texto_trx, idtrack_destino,
            idusuario_asignado, nomusuario_asignado, cod_perfil
        )
        VALUES (
            _idtrx, CURRENT_DATE, _idusuario, now(),
            _idperfil, _nomusuario, NULL,
            _idoperacion, _texto_trx, _idtrackflow,
            _idusuario_asignado, _nomusuario_asignado, _cod_perfil
        );

        -- Cargar checklist desde plantilla al ejecutar la transacción 133
        IF (_idtrx = 133) THEN
            INSERT INTO public.td_operacion_checklist (
                idoperacion, idtrack, orden,
                nombre_checklist, idtipodoc,
                documento_sistema, estado_checklist
            )
            SELECT
                _idoperacion,
                ct.idtrack,
                ct.orden,
                ct.nombre_checklist,
                ct.idtipodoc,
                NULL,
                0
            FROM public.td_checklist_track ct;
            --WHERE ct.idtrack = _idtrackflow;
        END IF;

		--Si la Transaccion es Emitir OS
		IF (_idtrx = 115) THEN
			UPDATE public.tm_operacion
		        SET
		            	fecha_emision = _fechaActual,
						fecha_emision_reg = _fechaActual,
						fecha_inicio = _fechaActual + INTERVAL '1 day',
						fecha_termino = (_fechaActual + INTERVAL '1 day') + (plazo * INTERVAL '1 day')
		        WHERE idoperacion = _idoperacion;
		end if;

        -- Si la transacción es la aprobación del Anexo 5, clonar como TDR (Anexo 3)
        IF (_idtrx = 123) THEN
            PERFORM public.fn_operacion_clonar_como_tdr(
                json_build_object(
                    'idoperacion', _idoperacion,
                    'idusuario', _idusuario
                )
            );
        END IF;

        _mensaje := json_build_object(
            'estado', 1,
            'idoperacion', _idoperacion,
            'mensaje', 'Se ejecutó la transacción satisfactoriamente'
        );

        PERFORM public.fn_insertar_td_operaciones_auditoria(
            json_build_object(
                'es_correcto', true,
                'accion', _accion,
                'nombre_tabla', 'public.tm_operacion',
                'funcion', 'public.fn_operacion_ejecutartrx',
                'parametro_entrada', _parametro,
                'salida', _mensaje,
                'ip', null,
                'usuario', _nomusuario
            )
        );

        RETURN _mensaje;

    EXCEPTION
        WHEN OTHERS THEN
            GET STACKED DIAGNOSTICS _error = MESSAGE_TEXT;

            PERFORM public.fn_insertar_td_operaciones_auditoria(
                json_build_object(
                    'es_correcto', false,
                    'accion', _accion,
                    'nombre_tabla', 'public.tm_operacion',
                    'funcion', 'public.fn_operacion_ejecutartrx',
                    'parametro_entrada', _parametro,
                    'salida', json_build_object(
                        'estado', 0,
                        'idoperacion', _idoperacion,
                        'mensaje', _error
                    ),
                    'ip', null,
                    'usuario', _nomusuario
                )
            );

            RETURN json_build_object(
                'estado', 0,
                'idoperacion', 0,
                'mensaje', _error
            );
    END;

END;
$function$
;

-- DROP FUNCTION public.fn_operacion_entregable_registrar_elementos(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_entregable_registrar_elementos(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _fechaActual    timestamp without time zone;
    _identregable   bigint;
    _idusuario_real int4;
    _usuario        varchar(100);
    _ip             varchar(100);
    _mensaje        json;
    _error          text;
    _elem           json;
BEGIN
    BEGIN
        _fechaActual    := now();
        _identregable   := (_parametro->>'identregable')::bigint;
        _idusuario_real := (_parametro->>'idusuario_real')::int4;
        _usuario        := _parametro->>'usuario_creacion';
        _ip             := _parametro->>'ip';

        -- Actualiza solo los campos "real" del entregable (cabecera)
        UPDATE public.tm_operacion_entregable
        SET
            idusuario_real         = _idusuario_real,
            fecentregable_real     = CURRENT_DATE,
            fecregentregable_real  = _fechaActual,
            documento_sistema      = _parametro->>'documento_sistema'
        WHERE identregable = _identregable;

        -- Inserta los elementos relacionados (siempre nuevos registros)
        FOR _elem IN SELECT * FROM json_array_elements(COALESCE(_parametro->'elementos', '[]'::json))
        LOOP
            INSERT INTO public.td_rel_entregable_elemento(
                identregable, idelemento, documento_sistema,
                usuario_creacion, fecha_creacion, activo
            )
            VALUES (
                _identregable,
                (_elem->>'idelemento')::bigint,
                _elem->>'documento_sistema',
                (_parametro->>'usuario_creacion')::int4,
                _fechaActual, true
            )
            ON CONFLICT (identregable, idelemento) DO NOTHING;
        END LOOP;

        _mensaje := json_build_object(
            'estado', 1,
            'identregable', _identregable,
            'mensaje', 'Se registró satisfactoriamente'
        );

        PERFORM public.fn_insertar_td_operaciones_auditoria(
            json_build_object(
                'es_correcto', true,
                'accion', 'RegistrarElementosEntregable',
                'nombre_tabla', 'public.td_rel_entregable_elemento',
                'funcion', 'public.fn_operacion_entregable_registrar_elementos',
                'parametro_entrada', _parametro,
                'salida', _mensaje,
                'ip', _ip,
                'usuario', _usuario
            )
        );

        RETURN _mensaje;

    EXCEPTION
        WHEN OTHERS THEN
            GET STACKED DIAGNOSTICS _error = MESSAGE_TEXT;

            PERFORM public.fn_insertar_td_operaciones_auditoria(
                json_build_object(
                    'es_correcto', false,
                    'accion', 'RegistrarElementosEntregable',
                    'nombre_tabla', 'public.td_rel_entregable_elemento',
                    'funcion', 'public.fn_operacion_entregable_registrar_elementos',
                    'parametro_entrada', _parametro,
                    'salida', json_build_object(
                        'estado', 0,
                        'identregable', _identregable,
                        'mensaje', _error
                    ),
                    'ip', _ip,
                    'usuario', _usuario
                )
            );

            RETURN json_build_object(
                'estado', 0,
                'identregable', 0,
                'mensaje', _error
            );
    END;

END;
$function$
;

-- DROP FUNCTION public.fn_operacion_esquela_listar(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_esquela_listar(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _idoperacion bigint;
BEGIN
    _idoperacion := (_parametro->>'idoperacion')::bigint;

    RETURN (
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (SELECT COUNT(*) FROM public.tm_operacion_esquela
                 WHERE idoperacion = _idoperacion AND activo = true) AS cantidad,
                COALESCE(
                    (
                        SELECT array_to_json(array_agg(esq ORDER BY esq.idtipoesquela))
                        FROM (
                            SELECT
                                e.idoperacion,
                                e.idtipoesquela,
                                e.idusuario_para,
                                e.idusuario_de,
                                e.asunto,
                                e.referencia,
                                e.expediente,
                                e.ref02,
                                e.ref03,
                                e.cuerpo,
                                e.activo
                            FROM public.tm_operacion_esquela e
                            WHERE e.idoperacion = _idoperacion
                              AND e.activo = true
                        ) esq
                    ), '[]'::json
                ) AS data
        ) resultado
    );

EXCEPTION WHEN OTHERS THEN
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_operacion_esquela_traeruno(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_esquela_traeruno(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _idoperacion   bigint;
    _idtipoesquela int4;
BEGIN
    _idoperacion   := (_parametro->>'idoperacion')::bigint;
    _idtipoesquela := (_parametro->>'idtipoesquela')::int4;

    RETURN (
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                e.idoperacion,
                e.idtipoesquela,
                e.idusuario_para,
                e.idusuario_de,
                e.asunto,
                e.referencia,
                e.expediente,
                e.ref02,
                e.ref03,
                e.cuerpo,
                e.activo,

				e.idusuario_para_dependencia,
				e.idusuario_de_dependencia,
				e.idusuario_jefe_de,
				e.idusuario_jefe_de_dependencia
            FROM public.tm_operacion_esquela e
            WHERE e.idoperacion   = _idoperacion
              AND e.idtipoesquela = _idtipoesquela
              AND e.activo = true
        ) resultado
    );

EXCEPTION WHEN OTHERS THEN
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_operacion_listar(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_listar(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _fecha_inicio date;
    _fecha_fin    date;
    _centro_costo varchar(15);
    _idperfil     int4;
    _iddocumento  int4;
    _idusuario    int4;
    _cod_uo       varchar(20);
BEGIN
    _fecha_inicio := (_parametro->>'fecha_inicio')::date;
    _fecha_fin    := (_parametro->>'fecha_fin')::date;
    _centro_costo := _parametro->>'centro_costo';
    _idperfil     := (_parametro->>'idperfil')::int4;
    _iddocumento  := COALESCE((_parametro->>'iddocumento')::int4, 0);
    _idusuario    := (_parametro->>'idusuario')::int4;
    _cod_uo       := _parametro->>'cod_uo';

    RETURN (
        WITH tmpData AS
        (
            SELECT
                op.idoperacion,
                op.nro_pedido,
                op.tiporegistro,
				'Nuevo' as nomtiporegistro,
                TO_CHAR(op.fecoperacion, 'DD/MM/YYYY') as fecoperacion,
                op.denominacion_contratacion,
                op.nombre_depend,
                op.centro_costo,
                op.cod_uo,
                op.idperfil,
                op.cod_uo_conformidad,
                op.cod_uo_informe,
                op.idestado,
                es.nomestado,
                op.idtrack,
                tr.nomtrack,
                tr.idfase,
                fa.nomfase,
                (tr.b_editar and (op.usuario_creacion = _idusuario or coalesce(op.idusuario_asignado,0) = _idusuario)) AS b_editar,
                tr.b_ver,
                --tr.b_sol_ua_opp,
                tr.b_rpta_opp_ua,
                tr.b_checklist_ut,
                tr.b_checklist_uc,
				tr.b_sol,
				tr.idtipoesquela,
                op.ruta_documento,
                op.ruta_documento_firma,
                op.idproveedor,
                pv.tipo AS tipo_proveedor,
                pv.razonsocial,
                pv.idtipodoc,
                pv.nrodocumento,
                pv.email,
				op.identrega_prov,
                COALESCE(
                    (
                        SELECT array_to_json(array_agg(trx))
                        FROM (
                            SELECT
                                t.idtrx,
                                t.nomtransaccion,
                                t.nomaccion,
                                rel.indresponsable,
                                rel.cod_perfil,
                                rel.indadjunto,
                                rel.indobservacion
                            FROM public.tm_rel_perfil_trackflow rel
                            INNER JOIN public.tm_transaccion t ON rel.idtrx = t.idtrx
                            WHERE rel.idtrack = op.idtrack
                              AND rel.idperfil = _idperfil
                        ) trx
                    ), '[]'::json
                ) AS transacciones
				,case when exists (
					select * FROM public.tm_proveedor_entregable pe
			        	LEFT JOIN public.tm_estado_entregable est ON est.idestado_entregable = pe.idestado_entregable
			        WHERE pe.idoperacion = op.idoperacion
			          AND pe.activo = true
						) then 'Entregable'
				else 'Requerimiento' end as tipoOperacion
            FROM public.tm_operacion op
            LEFT JOIN public.tm_estado_operacion es ON op.idestado = es.idestado
            LEFT JOIN public.tm_trackflow tr ON op.idtrack = tr.idtrack
            LEFT JOIN public.tm_fase fa ON tr.idfase = fa.idfase
            LEFT JOIN public.tm_proveedor pv ON op.idproveedor = pv.idproveedor
            WHERE
                op.activo = true
                AND op.fecoperacion BETWEEN _fecha_inicio AND _fecha_fin
                AND (_centro_costo IS NULL OR op.centro_costo = _centro_costo)
                AND (_iddocumento = 0 OR op.iddocumento = _iddocumento)
                AND (
                    (op.usuario_creacion = _idusuario or op.idusuario_asignado = _idusuario)
                    OR op.cod_uo IN (
                        SELECT cod_uo_hijo FROM public.td_uo_dependencias WHERE cod_uo = _cod_uo
                    )
                )
        )
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (SELECT count(idoperacion) FROM tmpData) AS cantidad,
                (
                    SELECT COALESCE(array_to_json(array_agg(tmpDat)), '[]'::json)
                    FROM (
                        SELECT
                            idoperacion,
                            nro_pedido,
							tiporegistro,
							nomtiporegistro,
                            fecoperacion,
                            denominacion_contratacion,
                            nombre_depend,
                            centro_costo,
                            cod_uo,
                            idperfil,
                            idestado,
                            nomestado,
                            idtrack,
                            nomtrack,
                            idfase,
                            nomfase,
                            b_editar,
                            b_ver,
                            --b_sol_ua_opp,
                            b_rpta_opp_ua,
                            b_checklist_ut,
                            b_checklist_uc,
							b_sol,
                            idtipoesquela,
                            ruta_documento,
                            ruta_documento_firma,
                            idproveedor,
                            tipo_proveedor,
                            razonsocial,
                            idtipodoc,
                            nrodocumento,
                            email,
							identrega_prov,
                            transacciones,
							tipoOperacion
                        FROM tmpData
                        ORDER BY fecoperacion DESC, idoperacion DESC
                    ) tmpDat
                ) AS data
        ) resultado
    );

END;
$function$
;

-- DROP FUNCTION public.fn_operacion_listar_por_fase(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_listar_por_fase(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _idfase   	int4;
    _idperfil	int4;
	_idusuario 	int4;
	_cod_uo 	varchar(80);
BEGIN
    _idfase   := (_parametro->>'idfase')::int4;
    _idperfil := (_parametro->>'idperfil')::int4;
 	_idusuario := (_parametro->>'idusuario')::int4;
	_cod_uo := (_parametro->>'cod_uo')::varchar(80);

    RETURN (
        WITH tmpData AS
        (
            SELECT
                op.idoperacion,
                op.nro_pedido,
                op.tiporegistro,
                op.fecoperacion,
                op.denominacion_contratacion,
                op.nombre_depend,
                op.centro_costo,
                op.cod_uo,
                op.idperfil,
                op.cod_uo_conformidad,
                op.cod_uo_informe,
                op.idestado,
                es.nomestado,
                op.idtrack,
                tr.nomtrack,
                tr.idfase,
                fa.nomfase,
                tr.b_editar,
                tr.b_ver,
                tr.b_sol_ua_opp,
                tr.b_rpta_opp_ua,
                tr.b_checklist_ut,
                tr.b_checklist_uc,
                tr.idtipoesquela,
				tr.b_sol,
                op.ruta_documento,
                op.ruta_documento_firma,
                op.idproveedor,
                pv.tipo AS tipo_proveedor,
                pv.razonsocial,
                pv.idtipodoc,
                pv.nrodocumento,
                pv.email,
                COALESCE(
                    (
                        SELECT array_to_json(array_agg(trx))
                        FROM (
                            SELECT
                                t.idtrx,
                                t.nomtransaccion,
                                t.nomaccion,
                                rel.indresponsable,
                                rel.cod_perfil,
                                rel.indadjunto,
                                rel.indobservacion
                            FROM public.tm_rel_perfil_trackflow rel
                            INNER JOIN public.tm_transaccion t ON rel.idtrx = t.idtrx
                            WHERE rel.idtrack = op.idtrack
                              AND rel.idperfil = _idperfil
                        ) trx
                    ), '[]'::json
                ) AS transacciones
            FROM public.tm_operacion op
            LEFT JOIN public.tm_estado_operacion es ON op.idestado = es.idestado
            LEFT JOIN public.tm_trackflow tr ON op.idtrack = tr.idtrack
            LEFT JOIN public.tm_fase fa ON tr.idfase = fa.idfase
            LEFT JOIN public.tm_proveedor pv ON op.idproveedor = pv.idproveedor
            WHERE
                op.activo = true
                AND tr.idfase = _idfase

                AND (
                    --(op.usuario_creacion = _idusuario or op.idusuario_asignado = _idusuario)
       
					--OR 
						tr.idfase IN ( select idfase from public.td_fase_perfil where idperfil = _idperfil
					)
                )
				AND ( --op.usuario_creacion = _idusuario
						--OR 
						(COALESCE(op.idusuario_asignado,0) =
						(CASE
							WHEN COALESCE(op.idusuario_asignado,0)>0 THEN _idusuario
							ELSE COALESCE(op.idusuario_asignado,0)
					
					    END))
						OR tr.idfase IN ( select idfase from public.td_fase_perfil where idperfil = _idperfil)
						--OR op.cod_uo IN (
				                        --SELECT cod_uo_hijo FROM public.td_uo_dependencias WHERE cod_uo = _cod_uo
				                    	--)
					)
        )
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (SELECT count(idoperacion) FROM tmpData) AS cantidad,
                (
                    SELECT COALESCE(array_to_json(array_agg(tmpDat)), '[]'::json)
                    FROM (
                        SELECT
                            idoperacion,
                            nro_pedido,
                            fecoperacion,
                            denominacion_contratacion,
                            nombre_depend,
                            centro_costo,
                            cod_uo,
                            idperfil,
                            idestado,
                            nomestado,
                            idtrack,
                            nomtrack,
                            idfase,
                            nomfase,
                            b_editar,
                            b_ver,
                            b_sol_ua_opp,
                            b_rpta_opp_ua,
                            b_checklist_ut,
                            b_checklist_uc,
							b_sol,
                            idtipoesquela,
                            ruta_documento,
                            ruta_documento_firma,
                            idproveedor,
                            tipo_proveedor,
                            razonsocial,
                            idtipodoc,
                            nrodocumento,
                            email,
                            transacciones
                        FROM tmpData
                        ORDER BY fecoperacion DESC, idoperacion DESC
                    ) tmpDat
                ) AS data
        ) resultado
    );

END;
$function$
;

-- DROP FUNCTION public.fn_operacion_listar_por_proveedor(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_listar_por_proveedor(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _idproveedor bigint;
    _idperfil    int4;
BEGIN
    _idproveedor := (_parametro->>'idproveedor')::bigint;
    _idperfil    := (_parametro->>'idperfil')::int4;

    RETURN (
        WITH tmpData AS
        (
            SELECT
                op.idoperacion,
                op.nro_pedido,
                op.tiporegistro,
                op.fecoperacion,
                op.denominacion_contratacion,
                op.nombre_depend,
                op.centro_costo,
                op.cod_uo,
                op.idperfil,
                op.cod_uo_conformidad,
                op.cod_uo_informe,
                op.idestado,
                es.nomestado,
                op.idtrack,
                tr.nomtrack,
                tr.idfase,
                fa.nomfase,
                tr.b_editar,
                tr.b_ver,
                tr.b_sol_ua_opp,
                tr.b_rpta_opp_ua,
                tr.b_checklist_ut,
                tr.b_checklist_uc,
                tr.idtipoesquela,
                op.ruta_documento,
                op.ruta_documento_firma,
                op.idproveedor,
                pv.tipo AS tipo_proveedor,
                pv.razonsocial,
                pv.idtipodoc,
                pv.nrodocumento,
                pv.email,
                COALESCE(
                    (
                        SELECT array_to_json(array_agg(trx))
                        FROM (
                            SELECT
                                t.idtrx,
                                t.nomtransaccion,
                                t.nomaccion,
                                rel.indresponsable,
                                rel.cod_perfil,
                                rel.indadjunto,
                                rel.indobservacion
                            FROM public.tm_rel_perfil_trackflow rel
                            INNER JOIN public.tm_transaccion t ON rel.idtrx = t.idtrx
                            WHERE rel.idtrack = op.idtrack
                              AND rel.idperfil = _idperfil
                        ) trx
                    ), '[]'::json
                ) AS transacciones
            FROM public.tm_operacion op
            LEFT JOIN public.tm_estado_operacion es ON op.idestado = es.idestado
            LEFT JOIN public.tm_trackflow tr ON op.idtrack = tr.idtrack
            LEFT JOIN public.tm_fase fa ON tr.idfase = fa.idfase
            LEFT JOIN public.tm_proveedor pv ON op.idproveedor = pv.idproveedor
            WHERE
                op.activo = true
                --AND op.idproveedor = _idproveedor
        )
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (SELECT count(idoperacion) FROM tmpData) AS cantidad,
                (
                    SELECT COALESCE(array_to_json(array_agg(tmpDat)), '[]'::json)
                    FROM (
                        SELECT
                            idoperacion,
                            nro_pedido,
                            fecoperacion,
                            denominacion_contratacion,
                            nombre_depend,
                            centro_costo,
                            cod_uo,
                            idperfil,
                            idestado,
                            nomestado,
                            idtrack,
                            nomtrack,
                            idfase,
                            nomfase,
                            b_editar,
                            b_ver,
                            b_sol_ua_opp,
                            b_rpta_opp_ua,
                            b_checklist_ut,
                            b_checklist_uc,
                            idtipoesquela,
                            ruta_documento,
                            ruta_documento_firma,
                            idproveedor,
                            tipo_proveedor,
                            razonsocial,
                            idtipodoc,
                            nrodocumento,
                            email,
                            transacciones
                        FROM tmpData
                        ORDER BY fecoperacion DESC, idoperacion DESC
                    ) tmpDat
                ) AS data
        ) resultado
    );

END;
$function$
;

-- DROP FUNCTION public.fn_operacion_traeruno(json);

CREATE OR REPLACE FUNCTION public.fn_operacion_traeruno(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _idoperacion bigint;
BEGIN
    _idoperacion := (_parametro->>'idoperacion')::bigint;

    RETURN (
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                op.idoperacion,
                op.iddocumento,
                op.fecoperacion,
                --op.nro_pedido,
				(SELECT string_agg(
                                p.nro_pedido,',')
                            FROM public.tm_operacion_pedido p
                            WHERE p.idoperacion = op.idoperacion
                              AND p.activo = true) as nro_pedido,
                op.ano_eje,
                op.tipo_pedido,
                op.tiporegistro,
				'Nuevo' as nomtiporegistro,
                op.centro_costo,
                op.cod_uo,
                op.idperfil,
                op.cod_uo_conformidad,
                op.cod_uo_informe,
                op.nombre_depend,
                op.abreviado_depend,
                op.actividad_operativa,
                op.meta_presupuestaria,
                op.denominacion_contratacion,
                op.plazo,
                op.cantidad_entregables,
                op.indproyecto,
                op.nombre_proyecto,
                op.monto_mensual,
                op.monto_total,
                op.idestado,
                es.nomestado,
                op.idtrack,
                tr.nomtrack,
                tr.idfase,
                fa.nomfase,
                tr.b_editar,
                tr.b_ver,
                tr.b_sol_ua_opp,
                tr.b_rpta_opp_ua,
                tr.b_checklist_ut,
                tr.b_checklist_uc,
                op.ruta_documento,
                op.ruta_documento_firma,
                op.idproveedor,
                pv.tipo AS tipo_proveedor,
                pv.razonsocial,
                pv.idtipodoc,
                pv.nrodocumento,
                pv.ruc,
                pv.celular,
                pv.apellido_paterno,
                pv.apellido_materno,
                pv.nombres,
                pv.email,
                op.activo,
                COALESCE(
                    (
                        SELECT array_to_json(array_agg(det))
                        FROM (
                            SELECT
                                d.idoperaciondet,
                                d.idseccion,
                                s.nomseccion,
                                d.nro_orden,
                                d.texto_seccion,
                                d.texto2_seccion,
                                d.texto3_seccion,
                                d.texto4_seccion,
                                d.indupdate,
                                d.activo,
                                COALESCE(
                                    (
                                        SELECT array_to_json(array_agg(el))
                                        FROM (
                                            SELECT
                                                e.idelemento,
                                                e.texto_elemento,
                                                e.cantidad,
                                                e.codelemento,
                                                e.unidad_medida,
                                                e.activo
                                            FROM public.td_elemento e
                                            WHERE e.idoperaciondet = d.idoperaciondet
                                              AND e.activo = true
                                        ) el
                                    ), '[]'::json
                                ) AS elementos,
                                COALESCE(
                                    (
                                        SELECT array_to_json(array_agg(ent))
                                        FROM (
                                            SELECT
                                                en.identregable,
                                                en.nomentregable,
                                                en.fecentregable_plan,
                                                en.idusuario_real,
                                                en.fecentregable_real,
                                                en.fecregentregable_real,
                                                en.dias_calendario,
                                                en.documento_sistema,
                                                en.activo,
                                                COALESCE(
                                                    (
                                                        SELECT array_to_json(array_agg(rel))
                                                        FROM (
                                                            SELECT
                                                                r.idelemento,
                                                                r.documento_sistema,
                                                                r.activo
                                                            FROM public.td_rel_entregable_elemento r
                                                            WHERE r.identregable = en.identregable
                                                              AND r.activo = true
                                                        ) rel
                                                    ), '[]'::json
                                                ) AS rel_elementos
                                            FROM public.tm_operacion_entregable en
                                            WHERE en.idoperaciondet = d.idoperaciondet
                                              AND en.activo = true
                                        ) ent
                                    ), '[]'::json
                                ) AS entregables
                            FROM public.td_operacion_detalle d
                            INNER JOIN public.tm_seccion s ON d.idseccion = s.idseccion
                            WHERE d.idoperacion = op.idoperacion
                              AND d.activo = true
                            ORDER BY d.nro_orden
                        ) det
                    ), '[]'::json
                ) AS detalle,
                COALESCE(
                    (
                        SELECT array_to_json(array_agg(ast))
                        FROM (
                            SELECT
                                a.idasset,
                                a.url_servicio,
                                a.cod_file,
                                a.idelemento,
                                a.identregable,
                                a.idfase,
                                a.idtrack,
                                a.idestado,
                                a.documento_sistema,
                                a.idtipoasset,
                                a.nomasset,
                                a.activo
                            FROM public.td_operacion_asset a
                            WHERE a.idoperacion = op.idoperacion
                              AND a.activo = true
                        ) ast
                    ), '[]'::json
                ) AS assets,
                COALESCE(
                    (
                        SELECT array_to_json(array_agg(ped))
                        FROM (
                            SELECT
                                p.idoperacion_pedido,
                                p.ano_eje,
                                p.nro_pedido,
                                p.actividad_operativa,
                                p.meta_presupuestaria,
                                p.ff_rb,
                                p.programa,
                                p.prod_py,
                                p.clasificador,
                                p.cod_item_pedido,
                                p.nom_item_pedido,
                                p.activo
                            FROM public.tm_operacion_pedido p
                            WHERE p.idoperacion = op.idoperacion
                              AND p.activo = true
                        ) ped
                    ), '[]'::json
                ) AS pedidos
            FROM public.tm_operacion op
			--join public.tm_operacion_pedido op_pedido on op.idoperacion = op_pedido.idoperacion
            LEFT JOIN public.tm_estado_operacion es ON op.idestado = es.idestado
            LEFT JOIN public.tm_trackflow tr ON op.idtrack = tr.idtrack
            LEFT JOIN public.tm_fase fa ON tr.idfase = fa.idfase
            LEFT JOIN public.tm_proveedor pv ON op.idproveedor = pv.idproveedor
            WHERE op.idoperacion = _idoperacion
        ) resultado
    );

END;
$function$
;

-- DROP FUNCTION public.fn_perfil_listar(json);

CREATE OR REPLACE FUNCTION public.fn_perfil_listar(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

BEGIN
    RETURN (
        WITH tmpData AS
        (
            SELECT
                p.idperfil,
                p.nomperfil
            FROM public.tm_perfil p
        )
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (SELECT count(*) FROM tmpData) AS cantidad,
                (
                    SELECT COALESCE(array_to_json(array_agg(tmpDat)), '[]'::json)
                    FROM (
                        SELECT
                            idperfil,
                            nomperfil
                        FROM tmpData
                        ORDER BY nomperfil
                    ) tmpDat
                ) AS data
        ) resultado
    );

END;
$function$
;

-- DROP FUNCTION public.fn_procesar_proveedor_entregable(json);

CREATE OR REPLACE FUNCTION public.fn_procesar_proveedor_entregable(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _identrega_prov  bigint;
    _idoperacion     bigint;
    _activo          bool;
    _idusuario       int4;
    _existe          bool;
    _fechaActual     timestamp := now();
    _det             json;
    _idrel           bigint;
    _mensaje         json;
BEGIN
    _identrega_prov := COALESCE((_parametro->>'identrega_prov')::bigint, 0);
    _idoperacion    := (_parametro->>'idoperacion')::bigint;
    _activo         := (_parametro->>'activo')::bool;
    _idusuario      := (_parametro->>'idusuario')::int4;

    SELECT EXISTS (
        SELECT 1 FROM public.tm_proveedor_entregable
        WHERE identrega_prov = _identrega_prov
    ) INTO _existe;

    IF (_activo = true) THEN

        IF (_identrega_prov = 0 OR _existe = false) THEN
            -- ----------------------------------------
            -- INSERT cabecera tm_proveedor_entregable
            -- ----------------------------------------
            INSERT INTO public.tm_proveedor_entregable (
                idoperacion,
                idproveedor,
                fecha_entrega,
				fecha_entrega_reg,
                idestado_entregable,
                dias_atraso,
                penalidad,
                observacion,
                descripcion,
                documento_sistema,
                activo,
                usuario_creacion,
                fecha_creacion
            )
            VALUES (
                _idoperacion,
                (_parametro->>'idproveedor')::int8,
                _fechaActual, --(_parametro->>'fecha_entrega')::date,
				_fechaActual,
                1, --(_parametro->>'idestado_entregable')::int4,
                COALESCE((_parametro->>'dias_atraso')::int4, 0),
                (_parametro->>'penalidad')::bpchar,
                _parametro->>'observacion',
                _parametro->>'descripcion',
                _parametro->>'documento_sistema',
                true,
                _idusuario,
                _fechaActual
            )
            RETURNING identrega_prov INTO _identrega_prov;

            -- ----------------------------------------
            -- INSERT detalle td_rel_entregable_elemento
            -- ----------------------------------------
            FOR _det IN SELECT * FROM json_array_elements(_parametro->'detalle')
            LOOP
                _idrel := (SELECT COALESCE(MAX(idrel_entregable_elemento), 0) + 1
                           FROM public.td_rel_entregable_elemento);

                INSERT INTO public.td_rel_entregable_elemento (
                    idrel_entregable_elemento,
                    identregable,
                    idelemento,
                    identrega_prov,
                    idtipodoc,
                    documento_sistema,
                    activo,
                    usuario_creacion,
                    fecha_creacion
                )
                VALUES (
                    _idrel,
                    (_det->>'identregable')::int8,
                    (_det->>'idelemento')::int8,
                    _identrega_prov,
                    (_det->>'idtipodoc')::int4,
                    _det->>'documento_sistema',
                    true,
                    _idusuario,
                    _fechaActual
                );
            END LOOP;

            -- ----------------------------------------
            -- UPDATE tm_operacion.identrega_prov
            -- ----------------------------------------
            UPDATE public.tm_operacion
            SET identrega_prov = _identrega_prov
            WHERE idoperacion = _idoperacion;

            _mensaje := json_build_object(
                'estado', 1,
                'identrega_prov', _identrega_prov,
                'mensaje', 'Entregable registrado correctamente'
            );

        ELSE
            -- ----------------------------------------
            -- UPDATE cabecera
            -- ----------------------------------------
            UPDATE public.tm_proveedor_entregable
            SET
                idproveedor          = (_parametro->>'idproveedor')::int8,
                fecha_entrega        = (_parametro->>'fecha_entrega')::date,
                idestado_entregable  = (_parametro->>'idestado_entregable')::int4,
                dias_atraso          = COALESCE((_parametro->>'dias_atraso')::int4, 0),
                penalidad            = (_parametro->>'penalidad')::bpchar,
                observacion          = _parametro->>'observacion',
                descripcion          = _parametro->>'descripcion',
                documento_sistema    = _parametro->>'documento_sistema',
                usuario_modificacion = _idusuario,
                fecha_modificacion   = _fechaActual
            WHERE identrega_prov = _identrega_prov;

            -- ----------------------------------------
            -- Reemplazar detalle: anular existentes e insertar nuevos
            -- ----------------------------------------
            UPDATE public.td_rel_entregable_elemento
            SET
                activo              = false,
                usuario_eliminacion = _idusuario,
                fecha_eliminacion   = _fechaActual
            WHERE identrega_prov = _identrega_prov;

            FOR _det IN SELECT * FROM json_array_elements(_parametro->'detalle')
            LOOP
                _idrel := (SELECT COALESCE(MAX(idrel_entregable_elemento), 0) + 1
                           FROM public.td_rel_entregable_elemento);

                INSERT INTO public.td_rel_entregable_elemento (
                    idrel_entregable_elemento,
                    identregable,
                    idelemento,
                    identrega_prov,
                    idtipodoc,
                    documento_sistema,
                    activo,
                    usuario_creacion,
                    fecha_creacion
                )
                VALUES (
                    _idrel,
                    (_det->>'identregable')::int8,
                    (_det->>'idelemento')::int8,
                    _identrega_prov,
                    (_det->>'idtipodoc')::int4,
                    _det->>'documento_sistema',
                    true,
                    _idusuario,
                    _fechaActual
                );
            END LOOP;

            _mensaje := json_build_object(
                'estado', 1,
                'identrega_prov', _identrega_prov,
                'mensaje', 'Entregable actualizado correctamente'
            );
        END IF;

    ELSE
        -- ----------------------------------------
        -- ANULAR
        -- ----------------------------------------
        UPDATE public.td_rel_entregable_elemento
        SET
            activo              = false,
            usuario_eliminacion = _idusuario,
            fecha_eliminacion   = _fechaActual
        WHERE identrega_prov = _identrega_prov;

        UPDATE public.tm_proveedor_entregable
        SET
            activo              = false,
            usuario_eliminacion = _idusuario,
            fecha_eliminacion   = _fechaActual
        WHERE identrega_prov = _identrega_prov;

        UPDATE public.tm_operacion
        SET identrega_prov = NULL
        WHERE idoperacion = _idoperacion;

        _mensaje := json_build_object(
            'estado', 1,
            'mensaje', 'Entregable anulado correctamente'
        );
    END IF;

    PERFORM public.fn_insertar_td_operaciones_auditoria(
        json_build_object(
            'es_correcto', true,
            'accion', CASE WHEN _activo = false THEN 'Anular'
                          WHEN _existe = false  THEN 'Registrar'
                          ELSE 'Modificar' END,
            'nombre_tabla', 'public.tm_proveedor_entregable',
            'funcion', 'public.fn_procesar_proveedor_entregable',
            'parametro_entrada', _parametro,
            'salida', _mensaje,
            'ip', null,
            'usuario', _idusuario::varchar
        )
    );

    RETURN _mensaje;

EXCEPTION WHEN OTHERS THEN
    PERFORM public.fn_insertar_td_operaciones_auditoria(
        json_build_object(
            'es_correcto', false,
            'accion', 'Procesar',
            'nombre_tabla', 'public.tm_proveedor_entregable',
            'funcion', 'public.fn_procesar_proveedor_entregable',
            'parametro_entrada', _parametro,
            'salida', json_build_object('estado', 0, 'mensaje', SQLERRM),
            'ip', null,
            'usuario', _idusuario::varchar
        )
    );
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_procesar_td_operacion_asset(json);

CREATE OR REPLACE FUNCTION public.fn_procesar_td_operacion_asset(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _fechaActual   timestamp without time zone;
    _idasset       bigint;
    _activo        bool;
    _usuario       varchar(100);
    _ip            varchar(100);
    _accion        varchar(100);
    _idelemento    bigint;
    _identregable  bigint;
    _mensaje       json;
    _error         text;
BEGIN
    BEGIN
        _fechaActual := now();
        _idasset     := (_parametro->>'idasset')::bigint;
        _activo      := (_parametro->>'activo')::bool;
        _ip          := _parametro->>'ip';

        _idelemento   := COALESCE((_parametro->>'idelemento')::bigint, 0);
        _identregable := COALESCE((_parametro->>'identregable')::bigint, 0);

        -- ===========================================
        -- REGISTRAR
        -- ===========================================
        IF (_idasset = 0 AND _activo = true) THEN

            _usuario := _parametro->>'usuario_creacion';
            _accion  := 'Registrar';

            INSERT INTO public.td_operacion_asset(
                idoperacion, url_servicio, cod_file,
                idelemento, identregable, idfase, idtrack, idestado,
                documento_sistema,
                usuario_creacion, fecha_creacion, activo
            )
            VALUES (
                (_parametro->>'idoperacion')::bigint,
                _parametro->>'url_servicio',
                _parametro->>'cod_file',
                _idelemento,
                _identregable,
                (_parametro->>'idfase')::int4,
                (_parametro->>'idtrack')::int4,
                (_parametro->>'idestado')::int4,
                _parametro->>'documento_sistema',
                (_parametro->>'usuario_creacion')::int4,
                _fechaActual, true
            )
            RETURNING idasset INTO _idasset;

            _mensaje := json_build_object(
                'estado', 1,
                'idasset', _idasset,
                'mensaje', 'Se registró satisfactoriamente'
            );

        -- ===========================================
        -- MODIFICAR
        -- ===========================================
        ELSIF (_idasset > 0 AND _activo = true) THEN

            _usuario := _parametro->>'usuario_modificacion';
            _accion  := 'Modificar';

            UPDATE public.td_operacion_asset
            SET
                idoperacion       = (_parametro->>'idoperacion')::bigint,
                url_servicio      = _parametro->>'url_servicio',
                cod_file          = _parametro->>'cod_file',
                idelemento        = _idelemento,
                identregable      = _identregable,
                idfase            = (_parametro->>'idfase')::int4,
                idtrack           = (_parametro->>'idtrack')::int4,
                idestado          = (_parametro->>'idestado')::int4,
                documento_sistema = _parametro->>'documento_sistema',
                usuario_modificacion = (_parametro->>'usuario_modificacion')::int4,
                fecha_modificacion   = _fechaActual
            WHERE idasset = _idasset;

            _mensaje := json_build_object(
                'estado', 1,
                'idasset', _idasset,
                'mensaje', 'Se actualizó el registro satisfactoriamente'
            );

        -- ===========================================
        -- ANULAR
        -- ===========================================
        ELSIF (_idasset > 0 AND _activo = false) THEN

            _usuario := _parametro->>'usuario_eliminacion';
            _accion  := 'Anular';

            UPDATE public.td_operacion_asset
            SET
                activo = false,
                usuario_eliminacion = (_parametro->>'usuario_eliminacion')::int4,
                fecha_eliminacion   = _fechaActual
            WHERE idasset = _idasset;

            _mensaje := json_build_object(
                'estado', 1,
                'idasset', _idasset,
                'mensaje', 'Se eliminó el registro satisfactoriamente'
            );

        END IF;

        PERFORM public.fn_insertar_td_operaciones_auditoria(
            json_build_object(
                'es_correcto', true,
                'accion', _accion,
                'nombre_tabla', 'public.td_operacion_asset',
                'funcion', 'public.fn_procesar_td_operacion_asset',
                'parametro_entrada', _parametro,
                'salida', _mensaje,
                'ip', _ip,
                'usuario', _usuario
            )
        );

        RETURN _mensaje;

    EXCEPTION
        WHEN OTHERS THEN
            GET STACKED DIAGNOSTICS _error = MESSAGE_TEXT;

            PERFORM public.fn_insertar_td_operaciones_auditoria(
                json_build_object(
                    'es_correcto', false,
                    'accion', _accion,
                    'nombre_tabla', 'public.td_operacion_asset',
                    'funcion', 'public.fn_procesar_td_operacion_asset',
                    'parametro_entrada', _parametro,
                    'salida', json_build_object(
                        'estado', 0,
                        'idasset', _idasset,
                        'mensaje', _error
                    ),
                    'ip', _ip,
                    'usuario', _usuario
                )
            );

            RETURN json_build_object(
                'estado', 0,
                'idasset', 0,
                'mensaje', 'No se pudo realizar el proceso'
            );
    END;

END;
$function$
;

-- DROP FUNCTION public.fn_procesar_tm_documento(json);

CREATE OR REPLACE FUNCTION public.fn_procesar_tm_documento(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _fechaActual timestamp without time zone;
    _iddocumento integer;
    _activo bool;
    _usuario varchar(100);
    _ip varchar(100);
    _accion varchar(100);
    _det json;
    _iddocumento_seccion bigint;
    _activo_det bool;
    _mensaje json;
    _error text;
BEGIN
    BEGIN
        _fechaActual := now();
        _iddocumento := (_parametro->>'iddocumento')::integer;
        _activo := (_parametro->>'activo')::bool;
        _ip := _parametro->>'ip';

        IF (_iddocumento = 0 AND _activo = true) THEN

            _usuario := _parametro->>'usuario_creacion';
            _accion := 'Registrar';

            INSERT INTO public.tm_documento(
                idtipodocumento,
                nomdocumento,
                usuario_creacion,
                fecha_creacion,
                activo
            )
            VALUES (
                (_parametro->>'idtipodocumento')::int2,
                _parametro->>'nomdocumento',
                (_parametro->>'usuario_creacion')::int4,
                _fechaActual,
                true
            )
            RETURNING iddocumento INTO _iddocumento;

            FOR _det IN SELECT * FROM json_array_elements(_parametro->'detalle')
            LOOP
                INSERT INTO public.td_documento_seccion(
                    iddocumento,
                    idseccion,
                    nro_orden,
                    texto_seccion,
                    texto2_seccion,
                    texto3_seccion,
                    texto4_seccion,
                    indelemento,
                    indentregable,
                    usuario_creacion,
                    fecha_creacion,
                    activo
                )
                VALUES (
                    _iddocumento,
                    (_det->>'idseccion')::int4,
                    (_det->>'nro_orden')::int2,
                    _det->>'texto_seccion',
                    _det->>'texto2_seccion',
                    _det->>'texto3_seccion',
                    _det->>'texto4_seccion',
                    CASE WHEN (_det->>'indelemento')::bool THEN B'1' ELSE B'0' END,
                    CASE WHEN (_det->>'indentregable')::bool THEN B'1' ELSE B'0' END,
                    (_parametro->>'usuario_creacion')::int4,
                    _fechaActual,
                    true
                );
            END LOOP;

            _mensaje := json_build_object(
                'estado', 1,
                'iddocumento', _iddocumento,
                'mensaje', 'Se registró satisfactoriamente'
            );

        ELSIF (_iddocumento > 0 AND _activo = true) THEN

            _usuario := _parametro->>'usuario_modificacion';
            _accion := 'Modificar';

            UPDATE public.tm_documento
            SET
                idtipodocumento = (_parametro->>'idtipodocumento')::int2,
                nomdocumento = _parametro->>'nomdocumento',
                usuario_modificacion = (_parametro->>'usuario_modificacion')::int4,
                fecha_modificacion = _fechaActual
            WHERE iddocumento = _iddocumento;

            FOR _det IN SELECT * FROM json_array_elements(_parametro->'detalle')
            LOOP
                _iddocumento_seccion := (_det->>'iddocumento_seccion')::bigint;
                _activo_det := (_det->>'activo')::bool;

                IF (_iddocumento_seccion = 0 AND _activo_det = true) THEN
                    -- Nueva sección
                    INSERT INTO public.td_documento_seccion(
                        iddocumento,
                        idseccion,
                        nro_orden,
                        texto_seccion,
                        texto2_seccion,
                        texto3_seccion,
                        texto4_seccion,
                        indelemento,
                        indentregable,
                        usuario_creacion,
                        fecha_creacion,
                        activo
                    )
                    VALUES (
                        _iddocumento,
                        (_det->>'idseccion')::int4,
                        (_det->>'nro_orden')::int2,
                        _det->>'texto_seccion',
                        _det->>'texto2_seccion',
                        _det->>'texto3_seccion',
                        _det->>'texto4_seccion',
                        CASE WHEN (_det->>'indelemento')::bool THEN B'1' ELSE B'0' END,
                        CASE WHEN (_det->>'indentregable')::bool THEN B'1' ELSE B'0' END,
                        (_parametro->>'usuario_modificacion')::int4,
                        _fechaActual,
                        true
                    );

                ELSIF (_iddocumento_seccion > 0 AND _activo_det = true) THEN
                    -- Actualizar sección existente
                    UPDATE public.td_documento_seccion
                    SET
                        idseccion = (_det->>'idseccion')::int4,
                        nro_orden = (_det->>'nro_orden')::int2,
                        texto_seccion = _det->>'texto_seccion',
                        texto2_seccion = _det->>'texto2_seccion',
                        texto3_seccion = _det->>'texto3_seccion',
                        texto4_seccion = _det->>'texto4_seccion',
                        indelemento = CASE WHEN (_det->>'indelemento')::bool THEN B'1' ELSE B'0' END,
                        indentregable = CASE WHEN (_det->>'indentregable')::bool THEN B'1' ELSE B'0' END,
                        usuario_modificacion = (_parametro->>'usuario_modificacion')::int4,
                        fecha_modificacion = _fechaActual,
                        indupdate = B'1'
                    WHERE iddocumento_seccion = _iddocumento_seccion;

                ELSIF (_iddocumento_seccion > 0 AND _activo_det = false) THEN
                    -- Anular sección
                    UPDATE public.td_documento_seccion
                    SET
                        activo = false,
                        usuario_eliminacion = (_parametro->>'usuario_modificacion')::int4,
                        fecha_eliminacion = _fechaActual
                    WHERE iddocumento_seccion = _iddocumento_seccion;

                END IF;
            END LOOP;

            _mensaje := json_build_object(
                'estado', 1,
                'iddocumento', _iddocumento,
                'mensaje', 'Se actualizó el registro satisfactoriamente'
            );

        ELSIF (_iddocumento > 0 AND _activo = false) THEN

            _usuario := _parametro->>'usuario_eliminacion';
            _accion := 'Anular';

            -- Anular primero el detalle
            UPDATE public.td_documento_seccion
            SET
                activo = false,
                usuario_eliminacion = (_parametro->>'usuario_eliminacion')::int4,
                fecha_eliminacion = _fechaActual
            WHERE iddocumento = _iddocumento
              AND activo = true;

            -- Anular cabecera
            UPDATE public.tm_documento
            SET
                activo = false,
                usuario_eliminacion = (_parametro->>'usuario_eliminacion')::int4,
                fecha_eliminacion = _fechaActual
            WHERE iddocumento = _iddocumento;

            _mensaje := json_build_object(
                'estado', 1,
                'iddocumento', _iddocumento,
                'mensaje', 'Se eliminó el registro satisfactoriamente'
            );

        END IF;

        PERFORM public.fn_insertar_td_operaciones_auditoria(
            json_build_object(
                'es_correcto', true,
                'accion', _accion,
                'nombre_tabla', 'public.tm_documento',
                'funcion', 'public.fn_procesar_tm_documento',
                'parametro_entrada', _parametro,
                'salida', _mensaje,
                'ip', _ip,
                'usuario', _usuario
            )
        );

        RETURN _mensaje;

    EXCEPTION
        WHEN OTHERS THEN
            GET STACKED DIAGNOSTICS _error = MESSAGE_TEXT;

            PERFORM public.fn_insertar_td_operaciones_auditoria(
                json_build_object(
                    'es_correcto', false,
                    'accion', _accion,
                    'nombre_tabla', 'public.tm_documento',
                    'funcion', 'public.fn_procesar_tm_documento',
                    'parametro_entrada', _parametro,
                    'salida', json_build_object(
                        'estado', 0,
                        'iddocumento', _iddocumento,
                        'mensaje', _error
                    ),
                    'ip', _ip,
                    'usuario', _usuario
                )
            );

            RETURN json_build_object(
                'estado', 0,
                'iddocumento', 0,
                'mensaje', 'No se pudo realizar el proceso'
            );
    END;

END;
$function$
;

-- DROP FUNCTION public.fn_procesar_tm_operacion(json);

CREATE OR REPLACE FUNCTION public.fn_procesar_tm_operacion(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _fechaActual       timestamp without time zone;
    _idoperacion       bigint;
    _activo            bool;
    _usuario           varchar(100);
    _ip                varchar(100);
    _accion            varchar(100);
    _mensaje           json;
    _error             text;

    _det               json;
    _idoperaciondet    bigint;
    _activo_det        bool;

    _elem              json;
    _idelemento        bigint;
    _activo_elem       bool;

    _entregable        json;
    _identregable      bigint;
    _activo_entregable bool;

    _rel               json;
    _activo_rel        bool;

    _asset             json;
    _idasset           bigint;
    _activo_asset      bool;
    _asset_idelemento  bigint;
    _asset_identregable bigint;

    _pedido            json;
    _idoperacion_pedido bigint;
    _activo_pedido     bool;

    _proveedor         json;
    _idproveedor        bigint;

    _cod_uo            varchar(20);
    _esjefe            bool;
BEGIN
    BEGIN
        _fechaActual := now();
        _idoperacion := (_parametro->>'idoperacion')::bigint;
        _activo      := (_parametro->>'activo')::bool;
        _ip          := _parametro->>'ip';

        -- Proveedor: si idproveedor=0 se registra uno nuevo, caso contrario se usa el existente
        -- (solo aplica para Registrar/Modificar, no para Anular)
        _idproveedor := 0;
        IF (_activo = true) THEN
            _proveedor    := _parametro->'proveedor';
            _idproveedor  := COALESCE((_proveedor->>'idproveedor')::bigint, 0);

            IF (_idproveedor = 0) THEN
                INSERT INTO public.tm_proveedor(
                    tipo, razonsocial, idtipodoc, nrodocumento, ruc, celular,
                    apellido_paterno, apellido_materno, nombres, email,
                    usuario_creacion, fecha_creacion, activo
                )
                VALUES (
                    _proveedor->>'tipo',
                    _proveedor->>'razonsocial',
                    _proveedor->>'idtipodoc',
                    _proveedor->>'nrodocumento',
                    _proveedor->>'ruc',
                    _proveedor->>'celular',
                    _proveedor->>'apellido_paterno',
                    _proveedor->>'apellido_materno',
                    _proveedor->>'nombres',
                    _proveedor->>'email',
                    (_parametro->>'usuario_creacion')::int4,
                    _fechaActual, true
                )
                RETURNING idproveedor INTO _idproveedor;
            END IF;
        END IF;

        -- ===========================================
        -- REGISTRAR
        -- ===========================================
        IF (_idoperacion = 0 AND _activo = true) THEN

            _usuario := _parametro->>'usuario_creacion';
            _accion  := 'Registrar';

            -- Determinar cod_uo según esjefe del perfil
            SELECT COALESCE(esjefe, false) INTO _esjefe
            FROM public.tm_perfil
            WHERE idperfil = (_parametro->>'idperfil')::int4;

            _cod_uo := _parametro->>'cod_uo';
            IF (_esjefe = false) THEN
                _cod_uo := _cod_uo || '.01';
            END IF;

            INSERT INTO public.tm_operacion(
                iddocumento, fecoperacion,
                nro_pedido, ano_eje, tipo_pedido,
                centro_costo, cod_uo, cod_uo_conformidad, cod_uo_informe, nombre_depend, abreviado_depend,
                actividad_operativa, meta_presupuestaria,
                denominacion_contratacion, plazo, idestado, idtrack, cantidad_entregables,
                indproyecto, nombre_proyecto, idproveedor, monto_mensual, monto_total,
                idperfil, tiporegistro, usuario_creacion, fecha_creacion, activo
            )
            VALUES (
                (_parametro->>'iddocumento')::int4,
                (_parametro->>'fecoperacion')::date,
                _parametro->>'nro_pedido',
                (_parametro->>'ano_eje')::int4,
                (_parametro->>'tipo_pedido')::bpchar,
                _parametro->>'centro_costo',
                _cod_uo,
                _parametro->>'cod_uo_conformidad',
                _parametro->>'cod_uo_informe',
                _parametro->>'nombre_depend',
                _parametro->>'abreviado_depend',
                _parametro->>'actividad_operativa',
                _parametro->>'meta_presupuestaria',
                _parametro->>'denominacion_contratacion',
                (_parametro->>'plazo')::int4,
                (_parametro->>'idestado')::int2,
                CASE WHEN (_parametro->>'iddocumento')::int4 = 5 THEN 51 ELSE 10 END,
                (_parametro->>'cantidad_entregables')::int4,
                (_parametro->>'indproyecto')::bool,
                _parametro->>'nombre_proyecto',
                _idproveedor,
                (_parametro->>'monto_mensual')::numeric,
                (_parametro->>'monto_total')::numeric,
                (_parametro->>'idperfil')::int4,
                (_parametro->>'tiporegistro')::int4,
                (_parametro->>'usuario_creacion')::int4,
                _fechaActual, true
            )
            RETURNING idoperacion INTO _idoperacion;

            FOR _det IN SELECT * FROM json_array_elements(_parametro->'detalle')
            LOOP
                INSERT INTO public.td_operacion_detalle(
                    idoperacion, idseccion, nro_orden, texto_seccion,
                    texto2_seccion, texto3_seccion, texto4_seccion,
                    usuario_creacion, fecha_creacion, activo
                )
                VALUES (
                    _idoperacion,
                    (_det->>'idseccion')::int4,
                    (_det->>'nro_orden')::int2,
                    _det->>'texto_seccion',
                    _det->>'texto2_seccion',
                    _det->>'texto3_seccion',
                    _det->>'texto4_seccion',
                    (_parametro->>'usuario_creacion')::int4,
                    _fechaActual, true
                )
                RETURNING idoperaciondet INTO _idoperaciondet;

                -- Elementos del detalle
                FOR _elem IN SELECT * FROM json_array_elements(_det->'elementos')
                LOOP
                    INSERT INTO public.td_elemento(
                        idoperaciondet, texto_elemento, cantidad,
                        codelemento, unidad_medida,
                        usuario_creacion, fecha_creacion, activo
                    )
                    VALUES (
                        _idoperaciondet,
                        _elem->>'texto_elemento',
                        (_elem->>'cantidad')::numeric,
                        _elem->>'codelemento',
                        _elem->>'unidad_medida',
                        (_parametro->>'usuario_creacion')::int4,
                        _fechaActual, true
                    )
                    RETURNING idelemento INTO _idelemento;
                END LOOP;

                -- Entregables del detalle
                FOR _entregable IN SELECT * FROM json_array_elements(_det->'entregables')
                LOOP
                    INSERT INTO public.tm_operacion_entregable(
                        idoperaciondet, nomentregable, fecentregable_plan,
                        idusuario_real, fecentregable_real, fecregentregable_real,
                        dias_calendario,
                        idusuarioreg, fecreg,
                        usuario_creacion, fecha_creacion, activo
                    )
                    VALUES (
                        _idoperaciondet,
                        _entregable->>'nomentregable',
                        (_entregable->>'fecentregable_plan')::date,
                        (_entregable->>'idusuario_real')::int4,
                        (_entregable->>'fecentregable_real')::date,
                        (_entregable->>'fecregentregable_real')::timestamp,
                        (_entregable->>'dias_calendario')::int4,
                        (_parametro->>'usuario_creacion')::int4,
                        _fechaActual,
                        (_parametro->>'usuario_creacion')::int4,
                        _fechaActual, true
                    )
                    RETURNING identregable INTO _identregable;

                    -- Relación entregable-elemento
                    FOR _rel IN SELECT * FROM json_array_elements(_entregable->'rel_elementos')
                    LOOP
                        INSERT INTO public.td_rel_entregable_elemento(
                            identregable, idelemento,
                            usuario_creacion, fecha_creacion, activo
                        )
                        VALUES (
                            _identregable,
                            (_rel->>'idelemento')::bigint,
                            (_parametro->>'usuario_creacion')::int4,
                            _fechaActual, true
                        );
                    END LOOP;
                END LOOP;

            END LOOP;

            -- Assets de la operación
            FOR _asset IN SELECT * FROM json_array_elements(COALESCE(_parametro->'assets', '[]'::json))
            LOOP
                INSERT INTO public.td_operacion_asset(
                    idoperacion, url_servicio, cod_file,
                    idelemento, identregable, idfase, idtrack, idestado,
                    documento_sistema, idtipoasset, nomasset,
                    usuario_creacion, fecha_creacion, activo
                )
                VALUES (
                    _idoperacion,
                    _asset->>'url_servicio',
                    _asset->>'cod_file',
                    COALESCE((_asset->>'idelemento')::bigint, 0),
                    COALESCE((_asset->>'identregable')::bigint, 0),
                    (_asset->>'idfase')::int4,
                    (_asset->>'idtrack')::int4,
                    (_asset->>'idestado')::int4,
                    _asset->>'documento_sistema',
                    (_asset->>'idtipoasset')::int4,
                    _asset->>'nomasset',
                    (_parametro->>'usuario_creacion')::int4,
                    _fechaActual, true
                );
            END LOOP;

            -- Pedidos de la operación
            FOR _pedido IN SELECT * FROM json_array_elements(COALESCE(_parametro->'pedidos', '[]'::json))
            LOOP
                INSERT INTO public.tm_operacion_pedido(
                    idoperacion, ano_eje, nro_pedido,
                    actividad_operativa, meta_presupuestaria,
                    ff_rb, programa, prod_py, clasificador,
                    cod_item_pedido, nom_item_pedido,
                    usuario_creacion, fecha_creacion, activo
                )
                VALUES (
                    _idoperacion,
                    (_pedido->>'ano_eje')::int4,
                    _pedido->>'nro_pedido',
                    _pedido->>'actividad_operativa',
                    _pedido->>'meta_presupuestaria',
                    _pedido->>'ff_rb',
                    _pedido->>'programa',
                    _pedido->>'prod_py',
                    _pedido->>'clasificador',
                    _pedido->>'cod_item_pedido',
                    _pedido->>'nom_item_pedido',
                    (_parametro->>'usuario_creacion')::int4,
                    _fechaActual, true
                );
            END LOOP;

            _mensaje := json_build_object(
                'estado', 1,
                'idoperacion', _idoperacion,
                'mensaje', 'Se registró satisfactoriamente'
            );

        -- ===========================================
        -- MODIFICAR
        -- ===========================================
        ELSIF (_idoperacion > 0 AND _activo = true) THEN

            _usuario := _parametro->>'usuario_creacion';
            _accion  := 'Modificar';

            UPDATE public.tm_operacion
            SET
                iddocumento          = (_parametro->>'iddocumento')::int4,
                fecoperacion         = (_parametro->>'fecoperacion')::date,
                nro_pedido           = _parametro->>'nro_pedido',
                ano_eje              = (_parametro->>'ano_eje')::int4,
                tipo_pedido          = (_parametro->>'tipo_pedido')::bpchar,
                centro_costo         = _parametro->>'centro_costo',
                cod_uo_conformidad   = _parametro->>'cod_uo_conformidad',
                cod_uo_informe       = _parametro->>'cod_uo_informe',
                nombre_depend        = _parametro->>'nombre_depend',
                abreviado_depend     = _parametro->>'abreviado_depend',
                actividad_operativa       = _parametro->>'actividad_operativa',
                meta_presupuestaria       = _parametro->>'meta_presupuestaria',
                denominacion_contratacion = _parametro->>'denominacion_contratacion',
                plazo                     = (_parametro->>'plazo')::int4,
                idestado                  = (_parametro->>'idestado')::int2,
                cantidad_entregables      = (_parametro->>'cantidad_entregables')::int4,
                indproyecto               = (_parametro->>'indproyecto')::bool,
                nombre_proyecto           = _parametro->>'nombre_proyecto',
                idproveedor               = _idproveedor,
                monto_mensual             = (_parametro->>'monto_mensual')::numeric,
                monto_total               = (_parametro->>'monto_total')::numeric,
                tiporegistro              = (_parametro->>'tiporegistro')::int4,
                usuario_modificacion      = (_parametro->>'usuario_creacion')::int4,
                fecha_modificacion   = _fechaActual
            WHERE idoperacion = _idoperacion;

            FOR _det IN SELECT * FROM json_array_elements(_parametro->'detalle')
            LOOP
                _idoperaciondet := (_det->>'idoperaciondet')::bigint;
                _activo_det     := (_det->>'activo')::bool;

                IF (_idoperaciondet = 0 AND _activo_det = true) THEN
                    -- Nuevo detalle
                    INSERT INTO public.td_operacion_detalle(
                        idoperacion, idseccion, nro_orden, texto_seccion,
                        texto2_seccion, texto3_seccion, texto4_seccion,
                        usuario_creacion, fecha_creacion, activo
                    )
                    VALUES (
                        _idoperacion,
                        (_det->>'idseccion')::int4,
                        (_det->>'nro_orden')::int2,
                        _det->>'texto_seccion',
                        _det->>'texto2_seccion',
                        _det->>'texto3_seccion',
                        _det->>'texto4_seccion',
                        (_parametro->>'usuario_creacion')::int4,
                        _fechaActual, true
                    )
                    RETURNING idoperaciondet INTO _idoperaciondet;

                ELSIF (_idoperaciondet > 0 AND _activo_det = true) THEN
                    -- Actualizar detalle existente
                    UPDATE public.td_operacion_detalle
                    SET
                        idseccion            = (_det->>'idseccion')::int4,
                        nro_orden            = (_det->>'nro_orden')::int2,
                        texto_seccion        = _det->>'texto_seccion',
                        texto2_seccion       = _det->>'texto2_seccion',
                        texto3_seccion       = _det->>'texto3_seccion',
                        texto4_seccion       = _det->>'texto4_seccion',
                        usuario_modificacion = (_parametro->>'usuario_creacion')::int4,
                        fecha_modificacion   = _fechaActual,
                        indupdate            = B'1'
                    WHERE idoperaciondet = _idoperaciondet;

                ELSIF (_idoperaciondet > 0 AND _activo_det = false) THEN
                    -- Anular detalle y sus hijos
                    UPDATE public.td_rel_entregable_elemento
                    SET activo = false,
                        usuario_eliminacion = (_parametro->>'usuario_creacion')::int4,
                        fecha_eliminacion   = _fechaActual
                    WHERE identregable IN (
                        SELECT identregable FROM public.tm_operacion_entregable
                        WHERE idoperaciondet = _idoperaciondet AND activo = true
                    );

                    UPDATE public.tm_operacion_entregable
                    SET activo = false,
                        usuario_eliminacion = (_parametro->>'usuario_creacion')::int4,
                        fecha_eliminacion   = _fechaActual
                    WHERE idoperaciondet = _idoperaciondet AND activo = true;

                    UPDATE public.td_elemento
                    SET activo = false,
                        usuario_eliminacion = (_parametro->>'usuario_creacion')::int4,
                        fecha_eliminacion   = _fechaActual
                    WHERE idoperaciondet = _idoperaciondet AND activo = true;

                    UPDATE public.td_operacion_detalle
                    SET activo = false,
                        usuario_eliminacion = (_parametro->>'usuario_creacion')::int4,
                        fecha_eliminacion   = _fechaActual
                    WHERE idoperaciondet = _idoperaciondet;

                    CONTINUE;
                END IF;

                -- Elementos del detalle
                FOR _elem IN SELECT * FROM json_array_elements(_det->'elementos')
                LOOP
                    _idelemento  := (_elem->>'idelemento')::bigint;
                    _activo_elem := (_elem->>'activo')::bool;

                    IF (_idelemento = 0 AND _activo_elem = true) THEN
                        INSERT INTO public.td_elemento(
                            idoperaciondet, texto_elemento, cantidad,
                            codelemento, unidad_medida,
                            usuario_creacion, fecha_creacion, activo
                        )
                        VALUES (
                            _idoperaciondet,
                            _elem->>'texto_elemento',
                            (_elem->>'cantidad')::numeric,
                            _elem->>'codelemento',
                            _elem->>'unidad_medida',
                            (_parametro->>'usuario_creacion')::int4,
                            _fechaActual, true
                        );

                    ELSIF (_idelemento > 0 AND _activo_elem = true) THEN
                        UPDATE public.td_elemento
                        SET
                            texto_elemento       = _elem->>'texto_elemento',
                            cantidad             = (_elem->>'cantidad')::numeric,
                            codelemento          = _elem->>'codelemento',
                            unidad_medida        = _elem->>'unidad_medida',
                            usuario_modificacion = (_parametro->>'usuario_creacion')::int4,
                            fecha_modificacion   = _fechaActual
                        WHERE idelemento = _idelemento;

                    ELSIF (_idelemento > 0 AND _activo_elem = false) THEN
                        UPDATE public.td_elemento
                        SET activo = false,
                            usuario_eliminacion = (_parametro->>'usuario_creacion')::int4,
                            fecha_eliminacion   = _fechaActual
                        WHERE idelemento = _idelemento;
                    END IF;
                END LOOP;

                -- Entregables del detalle
                FOR _entregable IN SELECT * FROM json_array_elements(_det->'entregables')
                LOOP
                    _identregable      := (_entregable->>'identregable')::bigint;
                    _activo_entregable := (_entregable->>'activo')::bool;

                    IF (_identregable = 0 AND _activo_entregable = true) THEN
                        INSERT INTO public.tm_operacion_entregable(
                            idoperaciondet, nomentregable, fecentregable_plan,
                            idusuario_real, fecentregable_real, fecregentregable_real,
                            dias_calendario,
                            idusuarioreg, fecreg,
                            usuario_creacion, fecha_creacion, activo
                        )
                        VALUES (
                            _idoperaciondet,
                            _entregable->>'nomentregable',
                            (_entregable->>'fecentregable_plan')::date,
                            (_entregable->>'idusuario_real')::int4,
                            (_entregable->>'fecentregable_real')::date,
                            (_entregable->>'fecregentregable_real')::timestamp,
                            (_entregable->>'dias_calendario')::int4,
                            (_parametro->>'usuario_creacion')::int4,
                            _fechaActual,
                            (_parametro->>'usuario_creacion')::int4,
                            _fechaActual, true
                        )
                        RETURNING identregable INTO _identregable;

                    ELSIF (_identregable > 0 AND _activo_entregable = true) THEN
                        UPDATE public.tm_operacion_entregable
                        SET
                            nomentregable        = _entregable->>'nomentregable',
                            fecentregable_plan   = (_entregable->>'fecentregable_plan')::date,
                            idusuario_real       = (_entregable->>'idusuario_real')::int4,
                            fecentregable_real   = (_entregable->>'fecentregable_real')::date,
                            fecregentregable_real = (_entregable->>'fecregentregable_real')::timestamp,
                            dias_calendario      = (_entregable->>'dias_calendario')::int4,
                            usuario_modificacion = (_parametro->>'usuario_creacion')::int4,
                            fecha_modificacion   = _fechaActual
                        WHERE identregable = _identregable;

                    ELSIF (_identregable > 0 AND _activo_entregable = false) THEN
                        UPDATE public.td_rel_entregable_elemento
                        SET activo = false,
                            usuario_eliminacion = (_parametro->>'usuario_creacion')::int4,
                            fecha_eliminacion   = _fechaActual
                        WHERE identregable = _identregable AND activo = true;

                        UPDATE public.tm_operacion_entregable
                        SET activo = false,
                            usuario_eliminacion = (_parametro->>'usuario_creacion')::int4,
                            fecha_eliminacion   = _fechaActual
                        WHERE identregable = _identregable;

                        CONTINUE;
                    END IF;

                    -- Relación entregable-elemento
                    FOR _rel IN SELECT * FROM json_array_elements(_entregable->'rel_elementos')
                    LOOP
                        _activo_rel := (_rel->>'activo')::bool;

                        IF (_activo_rel = true) THEN
                            INSERT INTO public.td_rel_entregable_elemento(
                                identregable, idelemento,
                                usuario_creacion, fecha_creacion, activo
                            )
                            VALUES (
                                _identregable,
                                (_rel->>'idelemento')::bigint,
                                (_parametro->>'usuario_creacion')::int4,
                                _fechaActual, true
                            )
                            ON CONFLICT (identregable, idelemento) DO NOTHING;

                        ELSE
                            UPDATE public.td_rel_entregable_elemento
                            SET activo = false,
                                usuario_eliminacion = (_parametro->>'usuario_creacion')::int4,
                                fecha_eliminacion   = _fechaActual
                            WHERE identregable = _identregable
                              AND idelemento   = (_rel->>'idelemento')::bigint;
                        END IF;
                    END LOOP;

                END LOOP;

            END LOOP;

            -- Assets de la operación
            FOR _asset IN SELECT * FROM json_array_elements(COALESCE(_parametro->'assets', '[]'::json))
            LOOP
                _idasset      := (_asset->>'idasset')::bigint;
                _activo_asset := (_asset->>'activo')::bool;

                _asset_idelemento   := COALESCE((_asset->>'idelemento')::bigint, 0);
                _asset_identregable := COALESCE((_asset->>'identregable')::bigint, 0);

                IF (_idasset = 0 AND _activo_asset = true) THEN
                    INSERT INTO public.td_operacion_asset(
                        idoperacion, url_servicio, cod_file,
                        idelemento, identregable, idfase, idtrack, idestado,
                        documento_sistema, idtipoasset, nomasset,
                        usuario_creacion, fecha_creacion, activo
                    )
                    VALUES (
                        _idoperacion,
                        _asset->>'url_servicio',
                        _asset->>'cod_file',
                        _asset_idelemento,
                        _asset_identregable,
                        (_asset->>'idfase')::int4,
                        (_asset->>'idtrack')::int4,
                        (_asset->>'idestado')::int4,
                        _asset->>'documento_sistema',
                        (_asset->>'idtipoasset')::int4,
                        _asset->>'nomasset',
                        (_parametro->>'usuario_creacion')::int4,
                        _fechaActual, true
                    );

                ELSIF (_idasset > 0 AND _activo_asset = true) THEN
                    UPDATE public.td_operacion_asset
                    SET
                        url_servicio      = _asset->>'url_servicio',
                        cod_file          = _asset->>'cod_file',
                        idelemento        = _asset_idelemento,
                        identregable      = _asset_identregable,
                        idfase            = (_asset->>'idfase')::int4,
                        idtrack           = (_asset->>'idtrack')::int4,
                        idestado          = (_asset->>'idestado')::int4,
                        documento_sistema = _asset->>'documento_sistema',
                        idtipoasset       = (_asset->>'idtipoasset')::int4,
                        nomasset          = _asset->>'nomasset',
                        usuario_modificacion = (_parametro->>'usuario_creacion')::int4,
                        fecha_modificacion   = _fechaActual
                    WHERE idasset = _idasset;

                ELSIF (_idasset > 0 AND _activo_asset = false) THEN
                    UPDATE public.td_operacion_asset
                    SET activo = false,
                        usuario_eliminacion = (_parametro->>'usuario_creacion')::int4,
                        fecha_eliminacion   = _fechaActual
                    WHERE idasset = _idasset;
                END IF;
            END LOOP;

            -- Pedidos de la operación
            FOR _pedido IN SELECT * FROM json_array_elements(COALESCE(_parametro->'pedidos', '[]'::json))
            LOOP
                _idoperacion_pedido := (_pedido->>'idoperacion_pedido')::bigint;
                _activo_pedido      := (_pedido->>'activo')::bool;

                IF (_idoperacion_pedido = 0 AND _activo_pedido = true) THEN
                    INSERT INTO public.tm_operacion_pedido(
                        idoperacion, ano_eje, nro_pedido,
                        actividad_operativa, meta_presupuestaria,
                        ff_rb, programa, prod_py, clasificador,
                        cod_item_pedido, nom_item_pedido,
                        usuario_creacion, fecha_creacion, activo
                    )
                    VALUES (
                        _idoperacion,
                        (_pedido->>'ano_eje')::int4,
                        _pedido->>'nro_pedido',
                        _pedido->>'actividad_operativa',
                        _pedido->>'meta_presupuestaria',
                        _pedido->>'ff_rb',
                        _pedido->>'programa',
                        _pedido->>'prod_py',
                        _pedido->>'clasificador',
                        _pedido->>'cod_item_pedido',
                        _pedido->>'nom_item_pedido',
                        (_parametro->>'usuario_creacion')::int4,
                        _fechaActual, true
                    );

                ELSIF (_idoperacion_pedido > 0 AND _activo_pedido = true) THEN
                    UPDATE public.tm_operacion_pedido
                    SET
                        ano_eje              = (_pedido->>'ano_eje')::int4,
                        nro_pedido           = _pedido->>'nro_pedido',
                        actividad_operativa  = _pedido->>'actividad_operativa',
                        meta_presupuestaria  = _pedido->>'meta_presupuestaria',
                        ff_rb                = _pedido->>'ff_rb',
                        programa             = _pedido->>'programa',
                        prod_py              = _pedido->>'prod_py',
                        clasificador         = _pedido->>'clasificador',
                        cod_item_pedido      = _pedido->>'cod_item_pedido',
                        nom_item_pedido      = _pedido->>'nom_item_pedido',
                        usuario_modificacion = (_parametro->>'usuario_creacion')::int4,
                        fecha_modificacion   = _fechaActual
                    WHERE idoperacion_pedido = _idoperacion_pedido;

                ELSIF (_idoperacion_pedido > 0 AND _activo_pedido = false) THEN
                    UPDATE public.tm_operacion_pedido
                    SET activo = false,
                        usuario_eliminacion = (_parametro->>'usuario_creacion')::int4,
                        fecha_eliminacion   = _fechaActual
                    WHERE idoperacion_pedido = _idoperacion_pedido;
                END IF;
            END LOOP;

            _mensaje := json_build_object(
                'estado', 1,
                'idoperacion', _idoperacion,
                'mensaje', 'Se actualizó el registro satisfactoriamente'
            );

        -- ===========================================
        -- ANULAR
        -- ===========================================
        ELSIF (_idoperacion > 0 AND _activo = false) THEN

            _usuario := _parametro->>'usuario_eliminacion';
            _accion  := 'Anular';

            -- 1. Anular relaciones entregable-elemento
            UPDATE public.td_rel_entregable_elemento
            SET activo = false,
                usuario_eliminacion = (_parametro->>'usuario_eliminacion')::int4,
                fecha_eliminacion   = _fechaActual
            WHERE identregable IN (
                SELECT e.identregable
                FROM public.tm_operacion_entregable e
                INNER JOIN public.td_operacion_detalle d ON e.idoperaciondet = d.idoperaciondet
                WHERE d.idoperacion = _idoperacion AND e.activo = true
            );

            -- 2. Anular entregables
            UPDATE public.tm_operacion_entregable
            SET activo = false,
                usuario_eliminacion = (_parametro->>'usuario_eliminacion')::int4,
                fecha_eliminacion   = _fechaActual
            WHERE idoperaciondet IN (
                SELECT idoperaciondet FROM public.td_operacion_detalle
                WHERE idoperacion = _idoperacion AND activo = true
            );

            -- 3. Anular elementos
            UPDATE public.td_elemento
            SET activo = false,
                usuario_eliminacion = (_parametro->>'usuario_eliminacion')::int4,
                fecha_eliminacion   = _fechaActual
            WHERE idoperaciondet IN (
                SELECT idoperaciondet FROM public.td_operacion_detalle
                WHERE idoperacion = _idoperacion AND activo = true
            );

            -- 4. Anular detalle
            UPDATE public.td_operacion_detalle
            SET activo = false,
                usuario_eliminacion = (_parametro->>'usuario_eliminacion')::int4,
                fecha_eliminacion   = _fechaActual
            WHERE idoperacion = _idoperacion AND activo = true;

            -- 5. Anular assets
            UPDATE public.td_operacion_asset
            SET activo = false,
                usuario_eliminacion = (_parametro->>'usuario_eliminacion')::int4,
                fecha_eliminacion   = _fechaActual
            WHERE idoperacion = _idoperacion AND activo = true;

            -- 6. Anular pedidos
            UPDATE public.tm_operacion_pedido
            SET activo = false,
                usuario_eliminacion = (_parametro->>'usuario_eliminacion')::int4,
                fecha_eliminacion   = _fechaActual
            WHERE idoperacion = _idoperacion AND activo = true;

            -- 7. Anular cabecera
            UPDATE public.tm_operacion
            SET activo = false,
                usuario_eliminacion = (_parametro->>'usuario_eliminacion')::int4,
                fecha_eliminacion   = _fechaActual
            WHERE idoperacion = _idoperacion;

            _mensaje := json_build_object(
                'estado', 1,
                'idoperacion', _idoperacion,
                'mensaje', 'Se eliminó el registro satisfactoriamente'
            );

        END IF;

        PERFORM public.fn_insertar_td_operaciones_auditoria(
            json_build_object(
                'es_correcto', true,
                'accion', _accion,
                'nombre_tabla', 'public.tm_operacion',
                'funcion', 'public.fn_procesar_tm_operacion',
                'parametro_entrada', _parametro,
                'salida', _mensaje,
                'ip', _ip,
                'usuario', _usuario
            )
        );

        RETURN _mensaje;

    EXCEPTION
        WHEN OTHERS THEN
            GET STACKED DIAGNOSTICS _error = MESSAGE_TEXT;

            PERFORM public.fn_insertar_td_operaciones_auditoria(
                json_build_object(
                    'es_correcto', false,
                    'accion', _accion,
                    'nombre_tabla', 'public.tm_operacion',
                    'funcion', 'public.fn_procesar_tm_operacion',
                    'parametro_entrada', _parametro,
                    'salida', json_build_object(
                        'estado', 0,
                        'idoperacion', _idoperacion,
                        'mensaje', _error
                    ),
                    'ip', _ip,
                    'usuario', _usuario
                )
            );

            RETURN json_build_object(
                'estado', 0,
                'idoperacion', 0,
                'mensaje', _error
            );
    END;

END;
$function$
;

-- DROP FUNCTION public.fn_procesar_tm_operacion_esquela(json);

CREATE OR REPLACE FUNCTION public.fn_procesar_tm_operacion_esquela(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _idoperacion    bigint;
    _idtipoesquela  int4;
    _activo         bool;
    _idusuario      int4;
    _existe         bool;
    _fechaActual    timestamp := now();
    _mensaje        json;
BEGIN
    _idoperacion   := (_parametro->>'idoperacion')::bigint;
    _idtipoesquela := (_parametro->>'idtipoesquela')::int4;
    _activo        := (_parametro->>'activo')::bool;
    _idusuario     := (_parametro->>'idusuario')::int4;

    SELECT EXISTS (
        SELECT 1 FROM public.tm_operacion_esquela
        WHERE idoperacion   = _idoperacion
          AND idtipoesquela = _idtipoesquela
    ) INTO _existe;

    IF (_activo = true) THEN

        IF (_existe = false) THEN
            INSERT INTO public.tm_operacion_esquela (
                idoperacion, idtipoesquela,
                idusuario_para, idusuario_de,
                asunto, referencia, expediente,
                ref02, ref03, cuerpo,
                activo, usuario_creacion, fecha_creacion
				,idusuario_para_dependencia,
				idusuario_de_dependencia,
				idusuario_jefe_de,
				idusuario_jefe_de_dependencia
            )
            VALUES (
                _idoperacion, _idtipoesquela,
                (_parametro->>'idusuario_para')::int4,
                (_parametro->>'idusuario_de')::int4,
                _parametro->>'asunto',
                _parametro->>'referencia',
                _parametro->>'expediente',
                _parametro->>'ref02',
                _parametro->>'ref03',
                _parametro->>'cuerpo',
                true, _idusuario, _fechaActual,
				NULLIF(_parametro->>'idusuario_para_dependencia', '')::int4,
				NULLIF(_parametro->>'idusuario_de_dependencia', '')::int4,
				NULLIF(_parametro->>'idusuario_jefe_de', '')::int4,
				NULLIF(_parametro->>'idusuario_jefe_de_dependencia', '')::int4
				/*_parametro->> 'idusuario_para_dependencia',
				_parametro->> 'idusuario_de_dependencia',
				_parametro->> 'idusuario_jefe_de',
				_parametro->> 'idusuario_jefe_de_dependencia'*/
            );

            _mensaje := json_build_object(
                'estado', 1,
                'mensaje', 'Esquela registrada correctamente'
            );
        ELSE
            UPDATE public.tm_operacion_esquela
            SET
                idusuario_para       = (_parametro->>'idusuario_para')::int4,
                idusuario_de         = (_parametro->>'idusuario_de')::int4,
                asunto               = _parametro->>'asunto',
                referencia           = _parametro->>'referencia',
                expediente           = _parametro->>'expediente',
                ref02                = _parametro->>'ref02',
                ref03                = _parametro->>'ref03',
                cuerpo               = _parametro->>'cuerpo',
                usuario_modificacion = _idusuario,
                fecha_modificacion   = _fechaActual,
				idusuario_para_dependencia =
				        NULLIF(_parametro->>'idusuario_para_dependencia', '')::int4,
			    idusuario_de_dependencia =
			        NULLIF(_parametro->>'idusuario_de_dependencia', '')::int4,
			    idusuario_jefe_de =
			        NULLIF(_parametro->>'idusuario_jefe_de', '')::int4,
			    idusuario_jefe_de_dependencia =
			        NULLIF(_parametro->>'idusuario_jefe_de_dependencia', '')::int4
				/*idusuario_para_dependencia 		= _parametro->> 'idusuario_para_dependencia',
				idusuario_de_dependencia 		= _parametro->> 'idusuario_de_dependencia',
				idusuario_jefe_de 				= _parametro->> 'idusuario_jefe_de',
				idusuario_jefe_de_dependencia 	= _parametro->> 'idusuario_jefe_de_dependencia'*/
            WHERE idoperacion   = _idoperacion
              AND idtipoesquela = _idtipoesquela;

            _mensaje := json_build_object(
                'estado', 1,
                'mensaje', 'Esquela actualizada correctamente'
            );
        END IF;

    ELSE
        UPDATE public.tm_operacion_esquela
        SET
            activo              = false,
            usuario_eliminacion = _idusuario,
            fecha_eliminacion   = _fechaActual
        WHERE idoperacion   = _idoperacion
          AND idtipoesquela = _idtipoesquela;

        _mensaje := json_build_object(
            'estado', 1,
            'mensaje', 'Esquela anulada correctamente'
        );
    END IF;

    PERFORM public.fn_insertar_td_operaciones_auditoria(
        json_build_object(
            'es_correcto', true,
            'accion', CASE WHEN _activo = false THEN 'Anular' WHEN _existe = false THEN 'Registrar' ELSE 'Modificar' END,
            'nombre_tabla', 'public.tm_operacion_esquela',
            'funcion', 'public.fn_procesar_tm_operacion_esquela',
            'parametro_entrada', _parametro,
            'salida', _mensaje,
            'ip', null,
            'usuario', _idusuario::varchar
        )
    );

    RETURN _mensaje;

EXCEPTION WHEN OTHERS THEN
    PERFORM public.fn_insertar_td_operaciones_auditoria(
        json_build_object(
            'es_correcto', false,
            'accion', 'Procesar',
            'nombre_tabla', 'public.tm_operacion_esquela',
            'funcion', 'public.fn_procesar_tm_operacion_esquela',
            'parametro_entrada', _parametro,
            'salida', json_build_object('estado', 0, 'mensaje', SQLERRM),
            'ip', null,
            'usuario', _idusuario::varchar
        )
    );
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_proveedor_entregable_listar(json);

CREATE OR REPLACE FUNCTION public.fn_proveedor_entregable_listar(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _idoperacion bigint;
BEGIN
    _idoperacion := (_parametro->>'idoperacion')::bigint;

    RETURN (
        SELECT json_build_object(
            'cantidad', COUNT(*),
            'data', COALESCE(
                json_agg(
                    json_build_object(
                        'identrega_prov',    pe.identrega_prov,
                        'fecha_entrega',     pe.fecha_entrega,
                        'fecha_revision',    pe.fecha_revision,
                        'fecha_revision_reg', pe.fecha_revision_reg,
                        'idestado_entregable', pe.idestado_entregable,
                        'nomestado_entregable', est.nomestado_entregable
                    )
                    ORDER BY pe.identrega_prov ASC, pe.fecha_entrega DESC
                ), '[]'::json
            )
        )
        FROM public.tm_proveedor_entregable pe
        LEFT JOIN public.tm_estado_entregable est ON est.idestado_entregable = pe.idestado_entregable
        WHERE pe.idoperacion = _idoperacion
          AND pe.activo = true
    );

EXCEPTION WHEN OTHERS THEN
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_proveedor_entregable_traeruno(json);

CREATE OR REPLACE FUNCTION public.fn_proveedor_entregable_traeruno(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _identrega_prov bigint;
BEGIN
    _identrega_prov := (_parametro->>'identrega_prov')::bigint;

    RETURN (
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                pe.identrega_prov,
                pe.idoperacion,
                pe.idproveedor,
                pe.fecha_entrega,
				TO_CHAR(pe.fecha_entrega, 'DD/MM/YYYY') as fecha_entrega_s,
                pe.idestado_entregable,
                pe.dias_atraso,
                pe.penalidad,
				case when pe.penalidad='S' then 'SI' else 'NO' end as nomPenalidad,
                pe.observacion,
                pe.descripcion,
                pe.documento_sistema,
                pe.activo,
				prov.apellido_paterno as nomProveedor,
				prov.ruc,
				ope.denominacion_contratacion as denominacionContratacion,
				est.nomestado_entregable as estadoEntregable,
				ope.monto_total,
				1 as numeroEntregable,
				1 as numeroPago,
				1 as montoEntregable,
				uo.nombre_unidad,
				fecha_emision ,
				fecha_emision_reg ,
				fecha_inicio ,
				fecha_termino,
                COALESCE(
                    (
                        SELECT array_to_json(array_agg(det ORDER BY det.idrel_entregable_elemento))
                        FROM (
                            SELECT
                                r.idrel_entregable_elemento,
                                r.identregable,
                                r.idelemento,
                                r.identrega_prov,
                                r.idtipodoc,
                                r.documento_sistema,
                                r.activo
                            FROM public.td_rel_entregable_elemento r
                            WHERE r.identrega_prov = _identrega_prov
                              AND r.activo = true
                        ) det
                    ), '[]'::json
                ) AS detalle,
				COALESCE(
                    (
                        SELECT array_to_json(array_agg(trx))
                        FROM (
                            SELECT
                                t.idtrx,
                                t.nomtransaccion,
                                t.nomaccion,
                                rel.indresponsable,
                                rel.cod_perfil,
                                rel.indadjunto,
                                rel.indobservacion
                            FROM public.tm_rel_perfil_trackflow rel
                            INNER JOIN public.tm_transaccion t ON rel.idtrx = t.idtrx
                            WHERE rel.idtrack = 0
                              AND rel.idperfil = 1 and rel.idtrx in (134,135)
                        ) trx
                    ), '[]'::json
                ) AS transacciones
            FROM public.tm_proveedor_entregable pe
			join public.tm_proveedor prov on pe.idproveedor = prov.idproveedor
			join public.tm_operacion ope on pe.idoperacion = ope.idoperacion
			left outer join public.tm_unidad_organizacional uo on ope.centro_costo = uo.centro_costo
			join public.tm_estado_entregable est on pe.idestado_entregable = est.idestado_entregable
            WHERE pe.identrega_prov = _identrega_prov
              AND pe.activo = true
        ) resultado
    );

EXCEPTION WHEN OTHERS THEN
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_rel_perfil_trackflow_guardar(json);

CREATE OR REPLACE FUNCTION public.fn_rel_perfil_trackflow_guardar(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _idperfil int4;
    _asig     json;
    _mensaje  json;
    _error    text;
BEGIN
    BEGIN
        _idperfil := (_parametro->>'idperfil')::int4;

        -- Reemplaza completamente la configuración del perfil
        DELETE FROM public.tm_rel_perfil_trackflow
        WHERE idperfil = _idperfil;

        FOR _asig IN SELECT * FROM json_array_elements(_parametro->'asignaciones')
        LOOP
            INSERT INTO public.tm_rel_perfil_trackflow(
                idperfil, idtrack, idtrx
            )
            VALUES (
                _idperfil,
                (_asig->>'idtrack')::int4,
                (_asig->>'idtrx')::int4
            )
            ON CONFLICT (idperfil, idtrack, idtrx) DO NOTHING;
        END LOOP;

        _mensaje := json_build_object(
            'estado', 1,
            'idperfil', _idperfil,
            'mensaje', 'Se guardó la configuración satisfactoriamente'
        );

        RETURN _mensaje;

    EXCEPTION
        WHEN OTHERS THEN
            GET STACKED DIAGNOSTICS _error = MESSAGE_TEXT;

            RETURN json_build_object(
                'estado', 0,
                'idperfil', 0,
                'mensaje', _error
            );
    END;

END;
$function$
;

-- DROP FUNCTION public.fn_rel_perfil_trackflow_matriz(json);

CREATE OR REPLACE FUNCTION public.fn_rel_perfil_trackflow_matriz(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _idperfil int4;
BEGIN
    _idperfil := (_parametro->>'idperfil')::int4;

    RETURN (
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (
                    SELECT COALESCE(array_to_json(array_agg(tf)), '[]'::json)
                    FROM (
                        SELECT
                            tr.idtrack,
                            tr.nomtrack,
                            tr.idfase,
                            fa.nomfase
                        FROM public.tm_trackflow tr
                        LEFT JOIN public.tm_fase fa ON tr.idfase = fa.idfase
                        ORDER BY fa.idfase, tr.idtrack
                    ) tf
                ) AS trackflows,
                (
                    SELECT COALESCE(array_to_json(array_agg(tx)), '[]'::json)
                    FROM (
                        SELECT
                            t.idtrx,
                            t.nomtransaccion,
                            t.nomaccion,
                            t.idtrackflow AS idtrackflow_destino
                        FROM public.tm_transaccion t
                        ORDER BY t.idtrx
                    ) tx
                ) AS transacciones,
                (
                    SELECT COALESCE(array_to_json(array_agg(rel)), '[]'::json)
                    FROM (
                        SELECT
                            r.idtrack,
                            r.idtrx
                        FROM public.tm_rel_perfil_trackflow r
                        WHERE r.idperfil = _idperfil
                    ) rel
                ) AS asignaciones
        ) resultado
    );

END;
$function$
;

-- DROP FUNCTION public.fn_siga_traer_unidad_organizacional(json);

CREATE OR REPLACE FUNCTION public.fn_siga_traer_unidad_organizacional(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _cod_uo varchar(20);
BEGIN
    _cod_uo := _parametro->>'cod_uo';

    RETURN (
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                uo.cod_uo,
                uo.centro_costo,
                uo.nombre_unidad,
                uo.siglas
            FROM public.tm_unidad_organizacional uo
            WHERE uo.cod_uo = _cod_uo
        ) resultado
    );

EXCEPTION WHEN OTHERS THEN
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_tipoesquela_traeruno(json);

CREATE OR REPLACE FUNCTION public.fn_tipoesquela_traeruno(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
DECLARE
    _idtipoesquela int4;
BEGIN
    _idtipoesquela := (_parametro->>'idtipoesquela')::int4;

    RETURN (
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                t.idtipoesquela,
                t.nombre_esquela,
                t.cod_uo_de,
                t.cod_uo_para,
                t.asunto,
                t.referencia,
                t.expediente,
                t.ref02,
                t.ref03,
                t.cuerpo,
                t.idtrack
            FROM public.tm_tipoesquela t
            WHERE t.idtipoesquela = _idtipoesquela
        ) resultado
    );

EXCEPTION WHEN OTHERS THEN
    RETURN json_build_object('estado', 0, 'mensaje', SQLERRM);
END;
$function$
;

-- DROP FUNCTION public.fn_transaccion_listar(json);

CREATE OR REPLACE FUNCTION public.fn_transaccion_listar(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

BEGIN
    RETURN (
        WITH tmpData AS
        (
            SELECT
                t.idtrx,
                t.nomtransaccion,
                t.idtrackflow,
                t.idestado,
                t.nomaccion
            FROM public.tm_transaccion t
        )
        SELECT row_to_json(resultado)
        FROM (
            SELECT
                (SELECT count(*) FROM tmpData) AS cantidad,
                (
                    SELECT COALESCE(array_to_json(array_agg(tmpDat)), '[]'::json)
                    FROM (
                        SELECT
                            idtrx,
                            nomtransaccion,
                            idtrackflow,
                            idestado,
                            nomaccion
                        FROM tmpData
                        ORDER BY idtrx
                    ) tmpDat
                ) AS data
        ) resultado
    );

END;
$function$
;