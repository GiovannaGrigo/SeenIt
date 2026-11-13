WITH allowed(table_name,column_name) AS (
 VALUES ('Users','Id'),
 ('UserSeries','Id'),('UserSeries','UserId'),('UserSeries','ExternalSeriesId'),('UserSeries','Name'),
 ('UserSeries','PosterUrl'),('UserSeries','Status'),('UserSeries','IsAnime'),('UserSeries','AddedAtUtc'),('UserSeries','UpdatedAtUtc'),
 ('EpisodiosAssistidos','Id'),('EpisodiosAssistidos','UserId'),('EpisodiosAssistidos','ExternalSeriesId'),
 ('EpisodiosAssistidos','ExternalEpisodeId'),('EpisodiosAssistidos','Temporada'),('EpisodiosAssistidos','NumeroEpisodio'),
 ('EpisodiosAssistidos','NomeEpisodio'),('EpisodiosAssistidos','ExternalCharacterId'),
 ('EpisodiosAssistidos','PersonagemFavoritoNome'),('EpisodiosAssistidos','AssistidoEmUtc'),
 ('EpisodiosAssistidosSentimentos','EpisodioAssistidoId'),('EpisodiosAssistidosSentimentos','Sentimento')
), relations AS (
 SELECT c.oid,c.relname,c.relkind,c.relowner,n.nspname FROM pg_catalog.pg_class c
 JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
 WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname !~ '^pg_toast'
), tables AS MATERIALIZED (
 SELECT oid,relname,relkind,relowner,nspname FROM relations WHERE relkind IN ('r','p','v','m','f')
), sequences AS MATERIALIZED (
 SELECT oid,relowner FROM relations WHERE relkind='S'
), roles AS (
 SELECT r.oid FROM pg_catalog.pg_roles r WHERE r.rolname=current_user
   OR pg_catalog.pg_has_role(r.oid,'MEMBER')
), database_acl AS MATERIALIZED (
 -- ACL provenance matters: has_database_privilege alone merges PUBLIC and roles.
 SELECT d.oid,a.grantee,a.privilege_type,a.is_grantable
 FROM pg_catalog.pg_database d
 CROSS JOIN LATERAL pg_catalog.aclexplode(
   coalesce(d.datacl,pg_catalog.acldefault('d',d.datdba))) a
)
SELECT 'administrative_roles' AS category,count(*)::bigint +
 (SELECT count(*) FROM pg_catalog.pg_auth_members m WHERE m.member IN (SELECT oid FROM roles) AND m.admin_option) AS violations FROM pg_catalog.pg_roles r
WHERE r.oid IN (SELECT oid FROM roles) AND
 (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls
  OR r.rolname IN ('postgres','service_role','supabase_admin','pg_read_all_data','pg_write_all_data','pg_monitor'))
UNION ALL
SELECT 'database',count(*) FROM pg_catalog.pg_database d
WHERE (d.datname=current_database() AND (pg_catalog.has_database_privilege(d.oid,'CREATE')
 OR d.datdba IN (SELECT oid FROM roles)
 -- Only non-delegable PUBLIC TEMP on the identity-verified database is exempt.
 OR (pg_catalog.has_database_privilege(d.oid,'TEMP') AND NOT EXISTS (
   SELECT 1 FROM database_acl a WHERE a.oid=d.oid AND a.grantee=0
    AND a.privilege_type='TEMPORARY' AND NOT a.is_grantable))
 -- Redundant direct or membership grants still block, including NOINHERIT/SET ROLE.
 OR EXISTS (SELECT 1 FROM database_acl a WHERE a.oid=d.oid
   AND a.privilege_type='TEMPORARY'
   AND (a.grantee IN (SELECT oid FROM roles) OR (a.grantee=0 AND a.is_grantable)))))
 OR (d.datallowconn AND d.datname<>current_database()
  AND pg_catalog.has_database_privilege(d.oid,'CONNECT,TEMP'))
UNION ALL
SELECT 'schema',count(*) FROM pg_catalog.pg_namespace n
WHERE pg_catalog.has_schema_privilege(n.oid,'CREATE') OR n.nspowner IN (SELECT oid FROM roles)
UNION ALL
SELECT 'relation_writes_or_ownership',count(*) FROM tables c WHERE
 c.relowner IN (SELECT oid FROM roles) OR
 (c.relkind IN ('r','p','v','m','f') AND (pg_catalog.has_table_privilege(c.oid,'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER,MAINTAIN')
  OR pg_catalog.has_any_column_privilege(c.oid,'INSERT,UPDATE,REFERENCES')))
UNION ALL
SELECT 'excess_select_or_grant_option',count(*) FROM tables c
JOIN pg_catalog.pg_attribute a ON a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
WHERE c.relkind IN ('r','p','v','m','f') AND
 ((pg_catalog.has_column_privilege(c.oid,a.attnum,'SELECT') AND NOT EXISTS (
   SELECT 1 FROM allowed x WHERE c.nspname='public' AND x.table_name=c.relname AND x.column_name=a.attname))
  OR pg_catalog.has_column_privilege(c.oid,a.attnum,'SELECT WITH GRANT OPTION'))
UNION ALL
SELECT 'missing_required_select',count(*) FROM allowed x
WHERE NOT EXISTS (SELECT 1 FROM tables c JOIN pg_catalog.pg_attribute a ON a.attrelid=c.oid
 WHERE c.nspname='public' AND c.relname=x.table_name AND a.attname=x.column_name AND a.attnum>0
 AND NOT a.attisdropped AND pg_catalog.has_column_privilege(c.oid,a.attnum,'SELECT'))
 OR NOT pg_catalog.has_schema_privilege('public','USAGE')
UNION ALL
SELECT 'sequence_access',count(*) FROM sequences c
WHERE pg_catalog.has_sequence_privilege(c.oid,'USAGE,SELECT,UPDATE') OR c.relowner IN (SELECT oid FROM roles)
UNION ALL
SELECT 'custom_routine_access_or_ownership',count(*) FROM pg_catalog.pg_proc p
JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace
WHERE (n.nspname NOT IN ('pg_catalog','information_schema') OR p.oid>=16384) AND
 (pg_catalog.has_function_privilege(p.oid,'EXECUTE') OR p.proowner IN (SELECT oid FROM roles))
UNION ALL
SELECT 'large_object',count(DISTINCT l.oid) FROM pg_catalog.pg_largeobject_metadata l
LEFT JOIN LATERAL pg_catalog.aclexplode(coalesce(l.lomacl,pg_catalog.acldefault('L',l.lomowner))) a ON true
WHERE l.lomowner IN (SELECT oid FROM roles) OR a.grantee=0 OR a.grantee IN (SELECT oid FROM roles)
UNION ALL
SELECT 'parameter_admin',count(*) FROM pg_catalog.pg_parameter_acl p
WHERE pg_catalog.has_parameter_privilege(p.parname,'SET,ALTER SYSTEM')
UNION ALL
SELECT 'foreign_access',count(*) FROM (
 SELECT 1 FROM pg_catalog.pg_foreign_server s WHERE pg_catalog.has_server_privilege(s.oid,'USAGE') OR s.srvowner IN (SELECT oid FROM roles)
 UNION ALL SELECT 1 FROM pg_catalog.pg_foreign_data_wrapper w WHERE pg_catalog.has_foreign_data_wrapper_privilege(w.oid,'USAGE') OR w.fdwowner IN (SELECT oid FROM roles)
) f
UNION ALL
SELECT 'tablespace_or_type_ownership',count(*) FROM (
 SELECT 1 FROM pg_catalog.pg_tablespace t WHERE pg_catalog.has_tablespace_privilege(t.oid,'CREATE') OR t.spcowner IN (SELECT oid FROM roles)
 UNION ALL SELECT 1 FROM pg_catalog.pg_type t WHERE t.typowner IN (SELECT oid FROM roles)
) t;
