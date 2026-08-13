ALTER TABLE scm.td_elemento
add column activo bool DEFAULT true NOT NULL, 
add column usuario_creacion int4 NULL, 
add column fecha_creacion timestamp NULL, 
add column usuario_modificacion int4 NULL, 
add column fecha_modificacion timestamp null,
add column usuario_eliminacion int4 NULL, 
add column fecha_eliminacion timestamp NULL;


ALTER TABLE scm.td_rel_entregable_elemento
add column activo bool DEFAULT true NOT NULL, 
add column usuario_creacion int4 NULL, 
add column fecha_creacion timestamp NULL, 
add column usuario_modificacion int4 NULL, 
add column fecha_modificacion timestamp null,
add column usuario_eliminacion int4 NULL, 
add column fecha_eliminacion timestamp NULL;

ALTER TABLE public.tm_operacion
add column cod_uo_conformidad varchar(80)   NULL;
COMMENT ON COLUMN public.tm_operacion.cod_uo_conformidad IS 'Codigo unidad organizacional de conformidad';


ALTER TABLE public.tm_operacion
add column cod_uo_informe varchar(80)   NULL;
COMMENT ON COLUMN public.tm_operacion.cod_uo_informe IS 'Codigo unidad organizacional de informe';

ALTER TABLE public.tm_trackflow
add column b_editar bool NULL, add column b_ver bool NULL, add column b_sol_ua_opp bool NULL, add column b_rpta_opp_ua bool NULL, add column b_checklist_ut bool NULL, add column b_checklist_uc bool NULL


-- public.tm_operacion_esquela definition

-- Drop table

-- DROP TABLE public.tm_operacion_esquela;






ALTER TABLE tm_operacion_esquela
asunto varchar NULL,
	referencia text NULL,
	expediente varchar NULL,
	ref02 varchar NULL,
	ref03 varchar NULL,
	cuerpo text NULL,
	idtrack int4 NULL; 
