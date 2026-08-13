--Proceso de Registro
select sumisiones.fn_procesar_td_sumisiones_evento_compensable(
'
{
	"id_evento_compensable":0,
	"id_sumision":7,
	"id_causa_contractual":10,
	"numero_evento_compensable":1,
	"numero_comunicacion_aconex":1,
	"es_impacto_costo":true,
	"es_impacto_plazo":true,
	"comentario":"comentario 1",
	"activo":true,
	"usuario_creacion":"45160794"
}
'
)
----Proceso de Modificar
select sumisiones.fn_procesar_td_sumisiones_evento_compensable(
'
{
	"id_evento_compensable":2,
	"id_sumision":7,
	"id_causa_contractual":11,
	"numero_evento_compensable":2,
	"numero_comunicacion_aconex":3,
	"es_impacto_costo":false,
	"es_impacto_plazo":false,
	"comentario":"comentario 2",
	"activo":true,
	"usuario_modificacion":"45160794"
}
'
)
--Proceso de Anular
select sumisiones.fn_procesar_td_sumisiones_evento_compensable(
'
{
	"id_evento_compensable":2,
	"activo":false,
	"usuario_eliminacion":"45160794"
}
'
)

CREATE OR REPLACE FUNCTION sumisiones.fn_procesar_td_sumisiones_evento_compensable(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _fechaActual timestamp without time zone;
    _id_evento_compensable integer;
	_activo bool;
	_usuario varchar(100);
	_ip varchar(100);
	_accion varchar(100);

    _mensaje json;
	_error text;
BEGIN
    BEGIN
        _fechaActual := now();
        _id_evento_compensable := (_parametro->>'id_evento_compensable')::integer;
		_activo := (_parametro->>'activo')::bool;		
		_ip:=_parametro->>'ip';

        IF (_id_evento_compensable = 0 AND _activo = true) THEN

			--Obtener usuario para el registro de auditoria
			_usuario:=_parametro->>'usuario_creacion';
			_accion:='Registrar';

			--Registrar Evento Compensable
			insert into sumisiones.td_sumisiones_evento_compensable(
				id_sumision,
				id_causa_contractual,
				numero_evento_compensable,
				numero_comunicacion_aconex,
				es_impacto_costo,
				es_impacto_plazo,
				comentario,
				usuario_creacion,
				fecha_creacion
			)
			select
				id_sumision,
				id_causa_contractual,
				numero_evento_compensable,
				numero_comunicacion_aconex,
				es_impacto_costo,
				es_impacto_plazo,
				comentario,
				usuario_creacion,
				_fechaActual
            from json_populate_record(NULL::sumisiones.td_sumisiones_evento_compensable, _parametro)
			returning id_evento_compensable into _id_evento_compensable;

            _mensaje := json_build_object
			(
                'estado', 1,
                'id_evento_compensable', _id_evento_compensable,
                'mensaje', 'Se registró satisfactoriamente'
            );

        elsif (_id_evento_compensable > 0 and _activo = true) then

			--Obtener usuario para el registro de auditoria
			_usuario:=_parametro->>'usuario_modificacion';
			_accion:='Modificar';

			--Modificar Evento Compensable
            update sumisiones.td_sumisiones_evento_compensable
            set
				id_causa_contractual=tmp.id_causa_contractual,
				numero_evento_compensable=tmp.numero_evento_compensable,
				numero_comunicacion_aconex=tmp.numero_comunicacion_aconex,
				es_impacto_costo=tmp.es_impacto_costo,
				es_impacto_plazo=tmp.es_impacto_plazo,
				comentario=tmp.comentario,
				usuario_modificacion=tmp.usuario_modificacion,
				fecha_modificacion=_fechaActual
            from (
	            select
					id_evento_compensable,
					id_sumision,
					id_causa_contractual,
					numero_evento_compensable,
					numero_comunicacion_aconex,
					es_impacto_costo,
					es_impacto_plazo,
					comentario,
					usuario_modificacion
	            from json_populate_record(NULL::sumisiones.td_sumisiones_evento_compensable, _parametro)
            ) tmp
            where td_sumisiones_evento_compensable.id_evento_compensable = tmp.id_evento_compensable;

            _mensaje := json_build_object(
                'estado', 1,
                'id_evento_compensable', _id_evento_compensable,
                'mensaje', 'Se actualizó el registro satisfactoriamente'
            );

        elsif (_id_evento_compensable > 0 AND _activo = false) THEN

			--Obtener usuario para el registro de auditoria
			_usuario:=_parametro->>'usuario_eliminacion';
			_accion:='Anular';

			--Anular Eventos Compensables
            update sumisiones.td_sumisiones_evento_compensable
            set
				activo=false,
				usuario_eliminacion=_usuario,
				fecha_eliminacion=_fechaActual
            where td_sumisiones_evento_compensable.id_evento_compensable = _id_evento_compensable;

            _mensaje := json_build_object(
                'estado', 1,
                'id_evento_compensable', _id_evento_compensable,
                'mensaje', 'Se eliminó el registro satisfactoriamente'
            );
			
        end if;

		--Registrar auditoria
		PERFORM sumisiones.fn_insertar_td_sumisiones_auditoria(
			json_build_object(
				'es_correcto',true,
				'accion',_accion,
			    'nombre_tabla','sumisiones.tm_sumisiones_evento_compensable',
				'funcion','sumisiones.fn_procesar_tm_sumisiones_evento_compensable',
				'parametro_entrada',_parametro,
				'salida',_mensaje,
				'ip',_ip,
				'usuario',_usuario
			)
		);

        return _mensaje;

    EXCEPTION
        WHEN OTHERS THEN
            GET STACKED DIAGNOSTICS _error = MESSAGE_TEXT;

			--Registrar auditoria
			PERFORM sumisiones.fn_insertar_td_sumisiones_auditoria(
				json_build_object(
					'es_correcto',false,
					'accion',_accion,
				    'nombre_tabla','sumisiones.tm_sumisiones_evento_compensable',
					'funcion','sumisiones.fn_procesar_tm_sumisiones_evento_compensable',
					'parametro_entrada',_parametro,
					'salida',
							json_build_object(
				                'estado', 0,
				                'id_evento_compensable', _id_evento_compensable,
				                'mensaje', _error
				            ),
					'ip',_ip,
					'usuario',_usuario
				)
			);

            RETURN json_build_object(
                'estado',0,
                'id_evento_compensable',0,
                'mensaje','No se pudo realizar el proceso'
            );
    END;

END;
$function$
;

--Listado
select sumisiones.fn_listar_tm_sumisiones_evento_compensable(
'
{
	"id_sumision":7
}
'
)
CREATE OR REPLACE FUNCTION sumisiones.fn_listar_tm_sumisiones_evento_compensable(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
    _id_sumision int;
	_limit int;
	_offset int;
BEGIN
	_id_sumision := _parametro ->> 'id_sumision';
	_limit := _parametro ->> 'limit';
	_offset := _parametro ->> 'offset';
	
		RETURN
		(
			WITH tmpData AS
			(
				select
					td_sumisiones_evento_compensable.id_evento_compensable,
					td_sumisiones_evento_compensable.id_sumision,
					td_sumisiones_evento_compensable.id_causa_contractual,
					td_sumisiones_causa_contractual.causa_contractual,
					td_sumisiones_evento_compensable.numero_evento_compensable,
					td_sumisiones_evento_compensable.numero_comunicacion_aconex,
					td_sumisiones_evento_compensable.es_impacto_costo,
					td_sumisiones_evento_compensable.es_impacto_plazo,
					td_sumisiones_evento_compensable.comentario
				from sumisiones.td_sumisiones_evento_compensable
				inner join sumisiones.td_sumisiones_causa_contractual on td_sumisiones_evento_compensable.id_causa_contractual = td_sumisiones_causa_contractual.id_causa_contractual				
				where
					td_sumisiones_evento_compensable.activo and
					td_sumisiones_evento_compensable.id_sumision =  _id_sumision

			)
	
			SELECT
				COALESCE(row_to_json(tmpData), '[]')
			FROM
			(
				select
				(
	
					SELECT
						count(id_evento_compensable)					
					FROM
						tmpData
				) AS cantidad,
				(
					SELECT
						COALESCE(array_to_json(array_agg(tmpDat)), '[]')
					FROM
					(
						SELECT 
							id_evento_compensable,
							id_sumision,
							id_causa_contractual,
							causa_contractual,
							numero_evento_compensable,
							numero_comunicacion_aconex,
							es_impacto_costo,
							es_impacto_plazo,
							comentario
						FROM
							tmpData
						order by
							id_evento_compensable desc
						limit _limit offset _offset
					) tmpDat
				) AS data
			) AS tmpData
		);

END;
$function$