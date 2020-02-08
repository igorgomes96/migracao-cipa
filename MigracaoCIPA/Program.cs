using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.IO;
using System.Linq;

namespace MigracaoCIPA
{
    class Program
    {
        private static MySqlConnection GetConnectionNew()
        {
            return new MySqlConnection("Server=mysql.solucoesti.online;DataBase=solucoesti02;Uid=solucoesti02;Pwd=C1p40nl1n3;");
        }

        private static MySqlConnection GetConnectionOld()
        {
            return new MySqlConnection("Server=mysql.solucoesti.online;DataBase=solucoesti;Uid=solucoesti;Pwd=C1p40nl1n3;");
        }

        static void Main(string[] args)
        {
            MigracaoSenha();
        }

        private static void MigracaoSenhaAdministradores()
        {
            DataTable dataTable;
            using (MySqlConnection connOld = GetConnectionOld())
            {
                connOld.Open();
                MySqlCommand cmd = new MySqlCommand("select * from usuarios_administradores where senha is not null", connOld);
                dataTable = ReadToDataTable(cmd);
            }

            using MySqlConnection conn = GetConnectionNew();
            conn.Open();
            using var transaction = conn.BeginTransaction();
            try
            {
                foreach (DataRow dr in dataTable.Rows)
                {
                    var senhaDescriptografada = CryptoHelper.Decrypt(dr["senha"].ToString());
                    var novaSenha = CryptoHelper.ComputeSha256Hash(senhaDescriptografada);

                    // Tenta pelo Login
                    MySqlCommand cmd = new MySqlCommand("update Usuarios set Senha = @Senha where Email = @Email", conn);
                    cmd.Parameters.AddWithValue("@Email", dr["email"]);
                    cmd.Parameters.AddWithValue("@Senha", novaSenha);
                    cmd.ExecuteNonQuery();

                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
            }
        }

        private static void MigracaoSenha()
        {
            DataTable dataTable;
            using (MySqlConnection connOld = GetConnectionOld())
            {
                connOld.Open();
                MySqlCommand cmd = new MySqlCommand(@"
                    select a.* from migracao_usuarios a
                    inner join funcionarios b on a.login = b.login
                    inner join funcionarios_eleicoes c on b.id = c.funcionario_id
                    where c.codigo_eleicao = 43", connOld);
                dataTable = ReadToDataTable(cmd);
            }

            int totalLinhas = 0;
            using MySqlConnection conn = GetConnectionNew();
            conn.Open();
            using var transaction = conn.BeginTransaction();
            try
            {
                foreach (DataRow dr in dataTable.Rows)
                {
                    var senhaDescriptografada = CryptoHelper.Decrypt(dr["senha"].ToString());
                    var novaSenha = CryptoHelper.ComputeSha256Hash(senhaDescriptografada);

                    // Tenta pelo Login
                    MySqlCommand cmd = new MySqlCommand("update Usuarios set Senha = @Senha where Email = @Email", conn);
                    cmd.Parameters.AddWithValue("@Email", dr["Login"]);
                    cmd.Parameters.AddWithValue("@Senha", novaSenha);
                    int rows = cmd.ExecuteNonQuery();

                    if (rows == 0)
                    {
                        // Tenta pelo Email
                        cmd = new MySqlCommand("update Usuarios set Senha = @Senha where Email = @Email", conn);
                        cmd.Parameters.AddWithValue("@Email", dr["email"]);
                        cmd.Parameters.AddWithValue("@Senha", novaSenha);
                        rows = cmd.ExecuteNonQuery();

                        if (rows == 0)
                        {
                            // Tenta pela Matrícula e pelo nome
                            cmd = new MySqlCommand(@"
                                update Usuarios a inner join Eleitores b on a.Id = b.UsuarioId set a.Senha = @Senha
                                where b.Matricula = @Matricula and b.Nome = @Nome
                            ", conn);
                            cmd.Parameters.AddWithValue("@Matricula", dr["matricula"]);
                            cmd.Parameters.AddWithValue("@Nome", dr["Nome"]);
                            cmd.Parameters.AddWithValue("@Senha", novaSenha);
                            rows = cmd.ExecuteNonQuery();
                        }
                    }
                    totalLinhas += rows;
                }
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
            }
            Console.WriteLine();
            Console.ReadKey();
        }

        private static DataTable ReadToDataTable(MySqlCommand cmd)
        {
            DataTable dataTable = new DataTable();
            using var dr = cmd.ExecuteReader();
            DataSet ds = new DataSet();
            ds.Tables.Add(dataTable);
            ds.EnforceConstraints = false;
            dataTable.Load(dr);
            return dataTable;
        }

        private static void MigracaoVotos()
        {
            DataTable dataTable;
            using (MySqlConnection connOld = GetConnectionOld())
            {
                connOld.Open();
                MySqlCommand cmd = new MySqlCommand("select * from migracao_votos where EleicaoId = 26", connOld);
                dataTable = ReadToDataTable(cmd);
            }

            using MySqlConnection conn = GetConnectionNew();
            conn.Open();
            using var transaction = conn.BeginTransaction();
            try
            {
                int votosDuplicados = 0;
                int linhas = 0;
                int eleitoresNaoEncontrados = 0;
                foreach (DataRow dr in dataTable.Rows)
                {
                    linhas++;
                    MySqlCommand cmd = new MySqlCommand(
                        @$"select Id from Eleitores where Matricula = '{dr["Matricula"].ToString()}' 
                            and EleicaoId = {dr["EleicaoId"].ToString()}", conn);
                    var dtEleitores = ReadToDataTable(cmd);
                    if (dtEleitores.Rows.Count > 1)
                        throw new Exception("Inconsistência nos eleitores!");

                    if (dtEleitores.Rows.Count == 1)
                    {
                        cmd = new MySqlCommand(
                            @$"insert into Votos (EleitorId, EleicaoId, IP, DataCadastro) values
                          (@EleitorId, @EleicaoId, @IP, @DataCadastro)", conn);

                        cmd.Parameters.AddWithValue("@EleitorId", dtEleitores.Rows[0]["Id"]);
                        cmd.Parameters.AddWithValue("@EleicaoId", dr["EleicaoId"]);
                        cmd.Parameters.AddWithValue("@IP", dr["IP"]);
                        cmd.Parameters.AddWithValue("@DataCadastro", dr["DataCadastro"]);
                    }
                    else
                    {
                        eleitoresNaoEncontrados++;
                    }

                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex) when (ex.Message.StartsWith("Duplicate entry"))
                    {
                        votosDuplicados++;
                    }

                }

                transaction.Commit();

            }
            catch (Exception ex)
            {
                transaction.Rollback();
            }
        }

        private static void MigracaoCandidatos()
        {
            DataTable dataTable;
            using (MySqlConnection connOld = GetConnectionOld())
            {
                connOld.Open();
                MySqlCommand cmd = new MySqlCommand("select * from migracao_candidatos where EleicaoId = 26", connOld);
                dataTable = ReadToDataTable(cmd);
            }

            using MySqlConnection conn = GetConnectionNew();
            conn.Open();
            using var transaction = conn.BeginTransaction();
            try
            {
                foreach (DataRow dr in dataTable.Rows)
                {
                    var foto = SalvarFoto(dr);
                    MySqlCommand cmd = new MySqlCommand(
                        @$"select Id from Eleitores where Matricula = '{dr["Matricula"].ToString()}' 
                            and EleicaoId = {dr["EleicaoId"].ToString()}", conn);
                    var dtEleitores = ReadToDataTable(cmd);
                    if (dtEleitores.Rows.Count != 1)
                        throw new Exception("Inconsistência nos eleitores!");

                    cmd = new MySqlCommand(
                        $@"insert into Inscricoes 
                        (Votos, StatusInscricao, EleitorId, EleicaoId, Foto, Objetivos, EmailAprovador, NomeAprovador,
                        HorarioAprovacao, DataCadastro, ResultadoApuracao) values 
                        (@Votos, @StatusInscricao, @EleitorId, @EleicaoId, @Foto, @Objetivos, @EmailAprovador, @NomeAprovador,
                        @HorarioAprovacao, @DataCadastro, @ResultadoApuracao)", conn
                    );
                    cmd.Parameters.AddWithValue("@Votos", dr["Votos"]);
                    cmd.Parameters.AddWithValue("@StatusInscricao", dr["StatusInscricao"]);
                    cmd.Parameters.AddWithValue("@EleitorId", dtEleitores.Rows[0]["Id"]);
                    cmd.Parameters.AddWithValue("@EleicaoId", dr["EleicaoId"]);
                    cmd.Parameters.AddWithValue("@Foto", $@"StaticFiles\Fotos\{foto}");
                    cmd.Parameters.AddWithValue("@Objetivos", dr["Objetivos"]);
                    cmd.Parameters.AddWithValue("@EmailAprovador", dr["EmailAprovador"]);
                    cmd.Parameters.AddWithValue("@NomeAprovador", dr["NomeAprovador"]);
                    cmd.Parameters.AddWithValue("@HorarioAprovacao", dr["HorarioAprovacao"]);
                    cmd.Parameters.AddWithValue("@DataCadastro", dr["DataCadastro"]);
                    cmd.Parameters.AddWithValue("@ResultadoApuracao", 0);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
            }
        }

        private static string SalvarFoto(DataRow dr)
        {
            Directory.CreateDirectory(dr["EleicaoId"].ToString());
            var arquivo = $@"{dr["EleicaoId"].ToString()}/{dr["Matricula"].ToString()} - {dr["Email"].ToString()}";
            var arquivoTemp = $"{arquivo}_temp.jpeg";
            File.WriteAllBytes(arquivoTemp, (byte[])dr["Foto"]);
            arquivo = $"{arquivo}.jpeg";
            ImageHelper.SalvarImagemJPEG(arquivoTemp, arquivo, 95);
            File.Delete(arquivoTemp);
            return arquivo;
        }

        private static void CorrigirMatriculaQueComecaComZero()
        {
            DataTable dataTable;
            using (MySqlConnection connOld = GetConnectionOld())
            {
                connOld.Open();
                MySqlCommand cmd = new MySqlCommand("select matricula, nome from funcionarios where matricula like '0%'", connOld);
                dataTable = ReadToDataTable(cmd);
            }

            using var conn = GetConnectionNew();
            conn.Open();
            int rows = 0;
            foreach (DataRow dr in dataTable.Rows)
            {
                string matriculaSem0 = new string(dr["Matricula"].ToString().SkipWhile(ch => ch == '0').ToArray());
                MySqlCommand cmdInsert = new MySqlCommand(
                    $"update Eleitores set Matricula = '{dr["Matricula"].ToString()}' where Nome = '{dr["nome"].ToString()}' and Matricula = '{matriculaSem0}'",
                    conn
                );
                rows += cmdInsert.ExecuteNonQuery();
            }
        }
    }
}
