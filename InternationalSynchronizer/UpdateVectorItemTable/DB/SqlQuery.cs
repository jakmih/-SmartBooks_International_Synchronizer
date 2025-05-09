namespace InternationalSynchronizer.UpdateVectorItemTable.DB
{
    class SqlQuery
    {
        public static string DatabaseIdQuery(string database)
        {
            return $"SELECT id FROM sb_database WHERE name = '{database}'";
        }

        public static string InsertDatabaseQuery(string database)
        {
            return $"INSERT INTO sb_database (name) VALUES ('{database}')";
        }

        public static string ClearVectorItemTable()
        {
            return "TRUNCATE TABLE vector_item";
        }

        public static string AllKnowledgesQuery()
        {
            return @"
            SELECT 
                sub.id, sub.name,
                pac.id, pac.name + ' - ' + pac.description,
                thm.id, thm.name,
                kno.id, kno.knowledge_text_preview, kno_t.id,
                sub.db_source
            FROM subject_type_union AS sub
            LEFT JOIN package_union AS pac
            ON sub.id = pac.id_subject_type
                AND sub.db_source = pac.db_source
                AND sub.name NOT LIKE '[*]IMPORT[*]%'
                AND pac.name NOT LIKE '[*]IMPORT[*]%'
                AND pac.date_deleted IS NULL
            LEFT JOIN theme_union AS thm
            ON pac.id = thm.id_package
                AND pac.db_source = thm.db_source
                AND thm.name NOT LIKE '[*]IMPORT[*]%'
                AND thm.date_deleted IS NULL
            LEFT JOIN theme_part_union AS thm_p
            ON thm_p.id_theme = thm.id
                AND thm.db_source = thm_p.db_source 
            LEFT JOIN knowledge_union AS kno
            ON kno.id_theme_part = thm_p.id
                AND thm_p.db_source = kno.db_source
                AND kno.knowledge_text_preview NOT LIKE '[*]IMPORT[*]%'
                AND kno.knowledge_text_preview IS NOT NULL
                AND kno.date_deleted IS NULL
            LEFT JOIN knowledge_type_union AS kno_t
            ON kno_t.id = kno.id_knowledge_type
                AND kno.db_source = kno_t.db_source
            ORDER BY sub.db_source, sub.id, pac.id, thm.id, kno.id";
        }
    }
}
