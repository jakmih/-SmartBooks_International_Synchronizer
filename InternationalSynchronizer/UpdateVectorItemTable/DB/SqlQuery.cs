namespace InternationalSynchronizer.UpdateVectorItemTable.DB
{
    class SqlQuery
    {
        public static string GetDatabaseIdQuery(string database)
        {
            return $"SELECT id FROM sb_database WHERE name = '{database}'";
        }

        public static string GetInsertDatabaseQuery(string database)
        {
            return $"INSERT INTO sb_database (name) VALUES ('{database}')";
        }

        public static string GetAllKnowledgesQuery()
        {
            return @"
            SELECT 
                sub.id, sub.name,
                pac.id, pac.name + ' - ' + pac.description,
                thm.id, thm.name,
                tsk.id, tsk.knowledge_text_preview, tsk_t.id,
                pac.date_deleted, thm.date_deleted, tsk.date_deleted
            FROM subject_type AS sub
            LEFT JOIN package AS pac ON sub.id = pac.id_subject_type
            LEFT JOIN theme AS thm ON pac.id = thm.id_package
            LEFT JOIN theme_part AS thm_p ON thm_p.id_theme = thm.id
            LEFT JOIN knowledge AS tsk ON tsk.id_theme_part = thm_p.id
            LEFT JOIN knowledge_type AS tsk_t ON tsk_t.id = tsk.id_knowledge_type
            ORDER BY sub.id, pac.id, thm.id, tsk.id";
        }
    }
}
