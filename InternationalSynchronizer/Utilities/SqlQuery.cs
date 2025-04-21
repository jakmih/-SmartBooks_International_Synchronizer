namespace InternationalSynchronizer.Utilities
{
    public static class SqlQuery
    {
        private static string GetSELECTQuery(Layer layer)
        {
            string query = "SELECT sub.name";
            if (layer == Layer.Subject)
                return query + ", sub.id";

            query += ", pac.name + ' - ' + pac.description";
            if (layer == Layer.Package)
                return query + ", pac.id";

            query += ", thm.name";
            if (layer == Layer.Theme)
                return query + ", thm.id";

            query += ", thm_p.name, tsk.knowledge_text_preview";
            if (layer == Layer.Knowledge)
                return query + ", tsk.id";

            return query + ", tsk_t.name, tsk.id";
        }

        private static string GetFROMQuery(Layer layer)
        {
            string query = " FROM subject_type AS sub";
            if (layer == Layer.Subject)
                return query;

            query += " INNER JOIN package AS pac ON sub.id = pac.id_subject_type";
            if (layer == Layer.Package)
                return query;

            query += " INNER JOIN theme AS thm ON pac.id = thm.id_package";
            if (layer == Layer.Theme)
                return query;

            query += " INNER JOIN theme_part AS thm_p ON thm_p.id_theme = thm.id";
            query += " INNER JOIN knowledge AS tsk ON tsk.id_theme_part = thm_p.id";
            if (layer == Layer.Knowledge)
                return query;

            query += " INNER JOIN knowledge_type AS tsk_t ON tsk_t.id = tsk.id_knowledge_type";
            return query;
        }

        private static string GetFilterQuery(Layer layer)
        {
            string query = " ";

            if (layer == Layer.Subject)
                return query;

            query += " AND pac.date_deleted IS NULL AND pac.name NOT LIKE '[*]IMPORT[*]%'";

            if (layer == Layer.Package)
                return query;

            query += " AND thm.date_deleted IS NULL AND thm.name NOT LIKE '[*]IMPORT[*]%'";

            if (layer == Layer.Theme)
                return query;

            query += " AND tsk.date_deleted IS NULL AND tsk.knowledge_text_preview NOT LIKE '[*]IMPORT[*]%'";
            return query;
        }

        public static string GetItemQuery(Layer layer, Int32 id, bool wholeLayer)
        {
            string query = GetSELECTQuery(layer) + GetFROMQuery(layer);

            string filterQuery = GetFilterQuery(layer);

            if (layer != Layer.KnowledgeType && !wholeLayer)
                layer++;

            switch (layer)
            {
                case Layer.Subject:
                    break;
                case Layer.Package:
                    query += " WHERE sub.id = " + id + filterQuery;
                    break;
                case Layer.Theme:
                    query += " WHERE pac.id = " + id + filterQuery;
                    break;
                case Layer.Knowledge:
                    query += " WHERE thm.id = " + id + filterQuery;
                    break;
                case Layer.KnowledgeType:
                    query += " WHERE tsk.id = " + id + filterQuery;
                    break;
            }
            return query;
        }

        public static string GetDatabaseIdQuery(string database)
        {
            return $"SELECT id FROM sb_database WHERE name = '{database}'";
        }

        public static string GetInsertDatabaseQuery(string database)
        {
            return $"INSERT INTO sb_database (name) VALUES ('{database}')";
        }

        public static string GetSyncPairQuery(Int32 layer, Int32 id, Int32 itemDatabaseId, Int32 pairItemDatabaseId)
        {
            return @$"
            SELECT sync_item_1.id_item, sync_item_2.id_item
            FROM sync_pair AS pair
            INNER JOIN sync_item AS sync_item_1 ON pair.id_sync_item_1 = sync_item_1.id
            INNER JOIN sync_item AS sync_item_2 ON pair.id_sync_item_2 = sync_item_2.id
            WHERE
                (
                    sync_item_1.id_item = {id}
                    AND sync_item_1.id_database = {itemDatabaseId}
                    AND sync_item_1.id_item_type = {layer}
                    AND sync_item_2.id_database = {pairItemDatabaseId}
                )
                OR
                (
                    sync_item_2.id_item = {id}
                    AND sync_item_2.id_database = {itemDatabaseId}
                    AND sync_item_2.id_item_type = {layer}
                    AND sync_item_1.id_database = {pairItemDatabaseId}
                )";
        }

        public static string GetSyncItemQuery(Int32 layer, Int32 id, Int32 databaseId)
        {
            return @$"
            SELECT id
            FROM sync_item
            WHERE
                id_database = {databaseId}
                AND id_item_type = {layer}
                AND id_item = {id}";
        }

        public static string GetInsertSyncItemQuery(Int32 layer, Int32 id, Int32 databaseId)
        {
            return $"INSERT INTO sync_item (id_item_type, id_item, id_database) VALUES ({layer}, {id}, {databaseId})";
        }

        public static string GetInsertSyncPairQuery(Int32 id1, Int32 id2)
        {
            return $"INSERT INTO sync_pair (id_sync_item_1, id_sync_item_2) VALUES ({id1}, {id2})";
        }

        public static string GetDeleteSyncPairQuery(Int32 id1, Int32 id2)
        {
            return @$"
            DELETE FROM sync_pair
            WHERE
                (id_sync_item_1 = {id1} AND id_sync_item_2 = {id2})
                OR
                (id_sync_item_1 = {id2} AND id_sync_item_2 = {id1})";
        }   
    }
}
