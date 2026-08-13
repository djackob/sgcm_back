CREATE TABLE scm.td_operaciones_auditoria (
	id_auditoria serial4 NOT NULL,
	es_correcto bool DEFAULT true NULL,
	accion varchar(100) NULL,
	nombre_tabla varchar(100) NULL,
	funcion varchar(100) NULL,
	parametro_entrada jsonb NULL,
	salida jsonb NULL,
	ip varchar(100) NULL,
	activo bool DEFAULT true NULL,
	usuario varchar(100) NULL,
	fecha timestamp NULL
);

CREATE OR REPLACE FUNCTION scm.fn_insertar_td_operaciones_auditoria(_parametro json)
 RETURNS json
 LANGUAGE plpgsql
AS $function$

DECLARE
	_fechaActual timestamp without time zone;
	_id_auditoria integer;

BEGIN
	_fechaActual := now();

	INSERT INTO scm.td_operaciones_auditoria
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
