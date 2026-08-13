--select login.fn_listar_tm_login_usuario_tipo_usuario_ultimo('{"id_usuario":1735}')
CREATE OR REPLACE FUNCTION login.fn_listar_tm_login_usuario_tipo_usuario_ultimo(
    _parametro json
)
RETURNS json
LANGUAGE plpgsql
AS $function$
DECLARE
    _id_usuario INT;
BEGIN
    _id_usuario := (_parametro ->> 'id_usuario')::INT;

    RETURN COALESCE(
        (
            SELECT row_to_json(tmpData)
            FROM (
                SELECT
                    u.id_usuario,
                    tu.codigo_tipo_usuario,
                    tu.nombre_tipo_usuario
                FROM login.tm_login_usuario u
                INNER JOIN login.td_login_usuario_tipo_usuario utu
                    ON u.id_usuario = utu.id_usuario
                    AND utu.activo = true
                INNER JOIN login.tm_login_tipo_usuario tu
                    ON utu.id_tipo_usuario = tu.id_tipo_usuario
                    AND tu.activo = true
                WHERE
                    u.activo = true
                    AND u.estado = true
                    AND u.id_usuario = _id_usuario
                ORDER BY utu.id_tipo_usuario DESC
                LIMIT 1
            ) tmpData
        ),
        '{}'::json
    );

END;
$function$;


--select login.fn_listar_tm_login_usuario_sistema_perfil('{"cod_sistema":"S0073","cod_perfil":"PE081"}')

CREATE OR REPLACE FUNCTION login.fn_listar_tm_login_usuario_sistema_perfil(
    _parametro json
)
RETURNS json
LANGUAGE plpgsql
AS $function$
DECLARE
    _cod_sistema varchar(10);
	_cod_perfil varchar(10);
BEGIN
    _cod_sistema := (_parametro ->> 'cod_sistema');
	_cod_perfil := (_parametro ->> 'cod_perfil');

    return
    (
		select
			COALESCE(array_to_json(array_agg(tmpData)), '[]')
		from
		(
			select
				tm_login_usuario.id_usuario,
				tm_login_usuario.apellido_paterno || ' ' || tm_login_usuario.apellido_materno || ' ' || tm_login_usuario.nombre as usuario
			from
				login.td_login_acceso
			INNER JOIN login.tm_login_usuario ON
				td_login_acceso.id_usuario = tm_login_usuario.id_usuario AND
				tm_login_usuario.activo
			INNER JOIN login.td_login_perfil_sistema ON
				td_login_acceso.id_perfil_sistema = td_login_perfil_sistema.id_perfil_sistema AND
				td_login_perfil_sistema.activo
			INNER JOIN login.td_login_sistema ON
				td_login_perfil_sistema.id_sistema = td_login_sistema.id_sistema
			INNER JOIN login.tm_login_perfil ON
				td_login_perfil_sistema.id_perfil = tm_login_perfil.id_perfil
			where
				td_login_sistema.cod_sistema = _cod_sistema and
				tm_login_perfil.cod_perfil = _cod_perfil
			order by
				usuario asc
		) as tmpData
    );

END;
$function$;