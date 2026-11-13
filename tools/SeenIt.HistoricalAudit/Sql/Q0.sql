SELECT 'table' AS kind,
       c.relname || '|' || c.relkind::text || '|' || c.relpersistence::text || '|' ||
       c.relrowsecurity::text || '|' || c.relforcerowsecurity::text || '|' ||
       EXISTS(SELECT 1 FROM pg_catalog.pg_trigger t WHERE t.tgrelid=c.oid AND NOT t.tgisinternal)::text || '|' ||
       EXISTS(SELECT 1 FROM pg_catalog.pg_rewrite w WHERE w.ev_class=c.oid)::text || '|' || c.relispartition::text AS signature
FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
WHERE n.nspname=@schema AND c.relname IN ('Users','UserSeries','EpisodiosAssistidos','EpisodiosAssistidosSentimentos')
UNION ALL
SELECT 'column', c.relname || '|' || a.attname || '|' || tn.nspname || '.' || t.typname || '|' ||
       a.attnotnull::text || '|' || CASE WHEN t.typname='varchar' THEN (a.atttypmod-4)::text ELSE '-' END || '|' ||
       a.attidentity::text || '|' || a.attgenerated::text || '|' ||
       coalesce(cn.nspname || '.' || co.collname,'-')
FROM pg_catalog.pg_attribute a JOIN pg_catalog.pg_class c ON c.oid=a.attrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
JOIN pg_catalog.pg_type t ON t.oid=a.atttypid
JOIN pg_catalog.pg_namespace tn ON tn.oid=t.typnamespace
LEFT JOIN pg_catalog.pg_collation co ON co.oid=a.attcollation
LEFT JOIN pg_catalog.pg_namespace cn ON cn.oid=co.collnamespace
WHERE n.nspname=@schema AND a.attnum>0 AND NOT a.attisdropped
 AND c.relname IN ('Users','UserSeries','EpisodiosAssistidos','EpisodiosAssistidosSentimentos')
UNION ALL
SELECT 'constraint', c.relname || '|' || k.contype::text || '|' ||
       coalesce((SELECT pg_catalog.string_agg(a.attname,',' ORDER BY u.ord)
         FROM pg_catalog.unnest(k.conkey) WITH ORDINALITY u(num,ord)
         JOIN pg_catalog.pg_attribute a ON a.attrelid=c.oid AND a.attnum=u.num),'') || '|' ||
       coalesce(nf.nspname,'') || '|' || coalesce(cf.relname,'') || '|' ||
       coalesce((SELECT pg_catalog.string_agg(a.attname,',' ORDER BY u.ord)
         FROM pg_catalog.unnest(k.confkey) WITH ORDINALITY u(num,ord)
         JOIN pg_catalog.pg_attribute a ON a.attrelid=k.confrelid AND a.attnum=u.num),'') || '|' ||
       k.confdeltype::text || '|' || k.confupdtype::text || '|' || k.confmatchtype::text || '|' ||
       k.convalidated::text || '|' || k.condeferrable::text || '|' || k.condeferred::text
FROM pg_catalog.pg_constraint k JOIN pg_catalog.pg_class c ON c.oid=k.conrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
LEFT JOIN pg_catalog.pg_class cf ON cf.oid=k.confrelid
LEFT JOIN pg_catalog.pg_namespace nf ON nf.oid=cf.relnamespace
WHERE n.nspname=@schema AND c.relname IN ('Users','UserSeries','EpisodiosAssistidos','EpisodiosAssistidosSentimentos')
UNION ALL
SELECT 'index', c.relname || '|' ||
       coalesce((SELECT pg_catalog.string_agg(a.attname,',' ORDER BY u.ord)
         FROM pg_catalog.unnest(i.indkey::smallint[]) WITH ORDINALITY u(num,ord)
         LEFT JOIN pg_catalog.pg_attribute a ON a.attrelid=c.oid AND a.attnum=u.num),'') || '|' ||
       i.indisunique::text || '|' || i.indisprimary::text || '|' || i.indisvalid::text || '|' || i.indisready::text || '|' ||
       (i.indpred IS NOT NULL)::text || '|' || (i.indexprs IS NOT NULL)::text || '|' ||
       (i.indnatts=i.indnkeyatts)::text || '|' || am.amname || '|' || i.indnullsnotdistinct::text || '|' ||
       (NOT EXISTS(SELECT 1 FROM pg_catalog.unnest(i.indclass::oid[]) op(oid)
         JOIN pg_catalog.pg_opclass oc ON oc.oid=op.oid
         JOIN pg_catalog.pg_namespace onsp ON onsp.oid=oc.opcnamespace
         WHERE NOT oc.opcdefault OR onsp.nspname<>'pg_catalog'))::text || '|' ||
       coalesce((SELECT pg_catalog.string_agg(coalesce(cn.nspname || '.' || co.collname,'-'),',' ORDER BY u.ord)
         FROM pg_catalog.unnest(i.indcollation::oid[]) WITH ORDINALITY u(oid,ord)
         LEFT JOIN pg_catalog.pg_collation co ON co.oid=u.oid
         LEFT JOIN pg_catalog.pg_namespace cn ON cn.oid=co.collnamespace),'') || '|' ||
       pg_catalog.array_to_string(i.indoption::smallint[],',') || '|' ||
       i.indimmediate::text || '|' || i.indislive::text || '|' || i.indcheckxmin::text || '|' ||
       coalesce((SELECT pg_catalog.string_agg(onsp.nspname || '.' || oc.opcname,',' ORDER BY u.ord)
         FROM pg_catalog.unnest(i.indclass::oid[]) WITH ORDINALITY u(oid,ord)
         JOIN pg_catalog.pg_opclass oc ON oc.oid=u.oid
         JOIN pg_catalog.pg_namespace onsp ON onsp.oid=oc.opcnamespace),'')
FROM pg_catalog.pg_index i JOIN pg_catalog.pg_class c ON c.oid=i.indrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
JOIN pg_catalog.pg_class ix ON ix.oid=i.indexrelid
JOIN pg_catalog.pg_am am ON am.oid=ix.relam
WHERE n.nspname=@schema AND c.relname IN ('Users','UserSeries','EpisodiosAssistidos','EpisodiosAssistidosSentimentos')
UNION ALL
SELECT 'fk_trigger', c.relname || '|' || cs.relname || '|' || ct.relname || '|' ||
       pn.nspname || '.' || p.proname || '|' || t.tgtype::text || '|' || t.tgenabled::text || '|' ||
       t.tgdeferrable::text || '|' || t.tginitdeferred::text
FROM pg_catalog.pg_trigger t
JOIN pg_catalog.pg_class c ON c.oid=t.tgrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
JOIN pg_catalog.pg_constraint k ON k.oid=t.tgconstraint AND k.contype='f'
JOIN pg_catalog.pg_class cs ON cs.oid=k.conrelid
JOIN pg_catalog.pg_class ct ON ct.oid=k.confrelid
JOIN pg_catalog.pg_proc p ON p.oid=t.tgfoid
JOIN pg_catalog.pg_namespace pn ON pn.oid=p.pronamespace
WHERE n.nspname=@schema AND t.tgisinternal
 AND c.relname IN ('Users','UserSeries','EpisodiosAssistidos','EpisodiosAssistidosSentimentos')
UNION ALL
SELECT 'default', c.relname || '|' || a.attname || '|' || pg_catalog.pg_get_expr(d.adbin,d.adrelid)
FROM pg_catalog.pg_attrdef d JOIN pg_catalog.pg_class c ON c.oid=d.adrelid
JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
JOIN pg_catalog.pg_attribute a ON a.attrelid=c.oid AND a.attnum=d.adnum
WHERE n.nspname=@schema AND c.relname IN ('Users','UserSeries','EpisodiosAssistidos','EpisodiosAssistidosSentimentos');
