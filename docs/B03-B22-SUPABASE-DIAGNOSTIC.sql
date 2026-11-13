-- B03-B2.2: MODELO PARA REVISÃO; NÃO EXECUTADO.
-- Somente metadados. Primeira passagem: operador autorizado.
-- Segunda passagem: autenticar como o auditor; nunca inferir seus direitos
-- a partir de uma sessão postgres ou de gestão. Stop-on-error no cliente.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL search_path = pg_catalog;
SET LOCAL statement_timeout = '5s';
SET LOCAL lock_timeout = '1s';
SET LOCAL idle_in_transaction_session_timeout = '10s';

SELECT current_setting('server_version') AS server_version,
       current_setting('server_version_num') AS server_version_num,
       current_database(), session_user, current_user,
       current_setting('transaction_read_only') AS read_only,
       current_setting('transaction_isolation') AS isolation,
       current_setting('search_path') AS search_path,
       current_setting('statement_timeout') AS statement_timeout,
       current_setting('lock_timeout') AS lock_timeout,
       current_setting('idle_in_transaction_session_timeout') AS idle_timeout,
       current_setting('session_replication_role') AS replication_role,
       has_database_privilege(current_database(), 'CONNECT') AS can_connect;
-- Os SET LOCAL acima não comprovam propagação de Options do Npgsql.
-- No teste de startup, executar este SELECT ANTES desses SET LOCAL.

-- Quatro linhas obrigatórias, relkind=r, RLS=false, FORCE=false.
WITH expected(name) AS (VALUES ('Users'),('UserSeries'),
 ('EpisodiosAssistidos'),('EpisodiosAssistidosSentimentos'))
SELECT e.name, c.oid IS NOT NULL AS present, c.relkind,
 c.relpersistence, c.relrowsecurity, c.relforcerowsecurity, c.relispartition,
 pg_get_userbyid(c.relowner) AS owner
FROM expected e LEFT JOIN pg_namespace n ON n.nspname='public'
LEFT JOIN pg_class c ON c.relnamespace=n.oid AND c.relname=e.name
ORDER BY e.name;

-- Estrutura e acesso efetivo, não valores. 29 colunas esperadas; 22 permitidas.
WITH allowed(table_name, cols) AS (VALUES
 ('Users', ARRAY['Id']),
 ('UserSeries', ARRAY['Id','UserId','ExternalSeriesId','Name','PosterUrl',
  'Status','IsAnime','AddedAtUtc','UpdatedAtUtc']),
 ('EpisodiosAssistidos', ARRAY['Id','UserId','ExternalSeriesId','ExternalEpisodeId',
  'Temporada','NumeroEpisodio','NomeEpisodio','ExternalCharacterId',
  'PersonagemFavoritoNome','AssistidoEmUtc']),
 ('EpisodiosAssistidosSentimentos', ARRAY['EpisodioAssistidoId','Sentimento']))
SELECT c.relname, a.attname, format_type(a.atttypid,a.atttypmod) AS type,
 a.attnotnull, a.attidentity, a.attgenerated,
 a.attname=ANY(x.cols) AS required_select,
 has_column_privilege(c.oid,a.attnum,'SELECT') AS effective_select,
 has_column_privilege(c.oid,a.attnum,'SELECT WITH GRANT OPTION') AS grant_option,
 has_column_privilege(c.oid,a.attnum,'INSERT,UPDATE,REFERENCES') AS excess_write,
 has_schema_privilege(n.oid,'USAGE') AS schema_usage
FROM allowed x JOIN pg_namespace n ON n.nspname='public'
JOIN pg_class c ON c.relnamespace=n.oid AND c.relname=x.table_name
JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
ORDER BY c.relname,a.attnum;

-- PUBLIC é grantee=0; ACL nula usa defaults PostgreSQL.
-- Inventário amplo sem chamar rotinas próprias. Não confundir PUBLIC com anon.
WITH objects(kind, object_name, acl) AS (
 SELECT 'database', quote_ident(datname), coalesce(datacl,acldefault('d',datdba)) FROM pg_database
 UNION ALL SELECT 'schema', quote_ident(nspname), coalesce(nspacl,acldefault('n',nspowner)) FROM pg_namespace
 UNION ALL SELECT CASE WHEN c.relkind='S' THEN 'sequence' ELSE 'relation' END,
  format('%I.%I',n.nspname,c.relname),
  coalesce(c.relacl,acldefault(CASE WHEN c.relkind='S' THEN 's'::"char" ELSE 'r'::"char" END,c.relowner))
 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE c.relkind IN ('r','p','v','m','f','S')
  AND n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname !~ '^pg_toast'
 UNION ALL SELECT 'column',format('%I.%I.%I',n.nspname,c.relname,a.attname),a.attacl
 FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid
 JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE a.attnum>0 AND NOT a.attisdropped AND a.attacl IS NOT NULL
 UNION ALL SELECT 'routine',p.oid::regprocedure::text,coalesce(p.proacl,acldefault('f',p.proowner))
 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
 WHERE n.nspname NOT IN ('pg_catalog','information_schema') OR p.oid>=16384
 UNION ALL SELECT 'large_object',oid::text,coalesce(lomacl,acldefault('L',lomowner)) FROM pg_largeobject_metadata
 UNION ALL SELECT 'parameter',parname,paracl FROM pg_parameter_acl
 UNION ALL SELECT 'server',srvname,coalesce(srvacl,acldefault('S',srvowner)) FROM pg_foreign_server
 UNION ALL SELECT 'wrapper',fdwname,coalesce(fdwacl,acldefault('F',fdwowner)) FROM pg_foreign_data_wrapper
 UNION ALL SELECT 'tablespace',spcname,coalesce(spcacl,acldefault('t',spcowner)) FROM pg_tablespace
 UNION ALL SELECT 'type',oid::regtype::text,coalesce(typacl,acldefault('T',typowner)) FROM pg_type
)
SELECT o.kind,o.object_name,a.privilege_type,a.is_grantable,
 pg_get_userbyid(a.grantor) AS grantor
FROM objects o CROSS JOIN LATERAL aclexplode(o.acl) a
WHERE a.grantee=0 ORDER BY o.kind,o.object_name,a.privilege_type;

-- Defaults futuros não substituem ACL atual; ausência de linha não elimina
-- os padrões embutidos de CONNECT/TEMP/EXECUTE do PostgreSQL.
SELECT pg_get_userbyid(d.defaclrole) AS creator,
 CASE WHEN d.defaclnamespace=0 THEN '(global)' ELSE n.nspname END AS schema_name,
 d.defaclobjtype,a.privilege_type,a.is_grantable
FROM pg_default_acl d LEFT JOIN pg_namespace n ON n.oid=d.defaclnamespace
CROSS JOIN LATERAL aclexplode(d.defaclacl) a
WHERE a.grantee=0 ORDER BY creator,schema_name,d.defaclobjtype;

-- Na sessão do auditor exigir atributos false e nenhuma membership.
SELECT rolname,rolsuper,rolinherit,rolcreaterole,rolcreatedb,rolcanlogin,
 rolreplication,rolbypassrls,rolconnlimit,rolvaliduntil
FROM pg_roles WHERE rolname=current_user;
SELECT r.rolname,pg_has_role(r.oid,'USAGE') AS inherited_now,
 pg_has_role(r.oid,'SET') AS can_set_role
FROM pg_roles r WHERE r.rolname<>current_user AND pg_has_role(r.oid,'MEMBER')
ORDER BY r.rolname;
SELECT pg_get_userbyid(m.roleid) AS granted_role,m.admin_option,
 m.inherit_option,m.set_option
FROM pg_auth_members m WHERE m.member=(SELECT oid FROM pg_roles WHERE rolname=current_user);

-- Pré-sondagem de todos os catálogos lidos por Q0, sem dados de aplicação.
-- LIMIT 0 confirma resolução/ACL, não conteúdo suficiente ou conformidade Q0.
SELECT c.oid FROM pg_class c CROSS JOIN pg_namespace n
 CROSS JOIN pg_attribute a CROSS JOIN pg_type t CROSS JOIN pg_collation co
 CROSS JOIN pg_constraint k CROSS JOIN pg_index i CROSS JOIN pg_am am
 CROSS JOIN pg_opclass op CROSS JOIN pg_trigger tr CROSS JOIN pg_proc p
 CROSS JOIN pg_attrdef d CROSS JOIN pg_rewrite rw LIMIT 0;
ROLLBACK;

-- Próximos passos separados, SOMENTE após autorização e perfil preliminar:
-- 1. Como auditor, transação curta READ ONLY com Privileges.sql original:
--    exatamente 12 categorias, todas zero; rollback confirmado.
-- 2. Depois, outra transação curta READ ONLY com Q0 original substituindo
--    apenas @schema por 'public'; comparar todas as 60 assinaturas.
-- Não incluir Q1-Q4 aqui. Sem executar funções próprias ou SQL de escrita.
