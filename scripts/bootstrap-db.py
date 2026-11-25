"""Provision isolated development credentials without printing secret values."""
import argparse
import os
from pathlib import Path
import re
import secrets
import stat
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
CONFIG = ROOT / ".env"
KEYS = ("SQL_ADMIN_PASSWORD", "SQL_APP_PASSWORD", "ORACLE_ADMIN_PASSWORD", "ORACLE_READER_PASSWORD")


def settings():
    try:
        fd = os.open(CONFIG, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError:
        pass
    else:
        with os.fdopen(fd, "w", encoding="ascii") as handle:
            handle.writelines(f"{key}=Aa1{secrets.token_hex(12)}\n" for key in KEYS)
    mode = CONFIG.lstat().st_mode
    if not stat.S_ISREG(mode) or (os.name == "posix" and mode & 0o077):
        raise SystemExit(".env deve ser arquivo regular privado, sem acesso de grupo/outros.")
    config = dict(line.split("=", 1) for line in CONFIG.read_text(encoding="ascii").splitlines())
    if set(config) != set(KEYS) or any(not re.fullmatch(r"Aa1[0-9a-f]{24}", config[key]) for key in KEYS):
        raise SystemExit(".env incompatível com o laboratório; valores não exibidos.")
    return config


def init_sql(config):
    admin = 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec /opt/mssql-tools18/bin/sqlcmd -U sa -S localhost -C -b'

    def sql(script, target="master"):
        result = subprocess.run(["docker", "compose", "exec", "-T", "sql", "sh", "-c", admin + " -d " + target],
                                cwd=ROOT, input=script, text=True, capture_output=True)
        if result.returncode:
            raise SystemExit(f"Falha ao provisionar SQL Server (código {result.returncode}); consulte docker compose logs sql.")

    password = config["SQL_APP_PASSWORD"]
    sql("IF DB_ID(N'FechaTurma') IS NULL CREATE DATABASE FechaTurma;\nGO\n"
        "IF SUSER_ID(N'fechaturma_runtime') IS NULL CREATE LOGIN fechaturma_runtime "
        f"WITH PASSWORD=N'{password}', CHECK_POLICY=ON;\nGO\n")
    schema = (ROOT / "database/sqlserver/001_closures.sql").read_text(encoding="utf-8")
    sql(schema + "\nGO\nIF USER_ID(N'fechaturma_runtime') IS NULL BEGIN\n"
        "CREATE USER fechaturma_runtime FOR LOGIN fechaturma_runtime;\n"
        "ALTER ROLE fechaturma_app ADD MEMBER fechaturma_runtime;\nEND;\nGO\n", "FechaTurma")
    print("Banco FechaTurma provisionado; usuário da aplicação sem permissões de DDL.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--init-sql", action="store_true")
    group.add_argument("--exec", nargs=argparse.REMAINDER, metavar="COMMAND")
    args = parser.parse_args()
    config = settings()
    if args.init_sql:
        init_sql(config)
    elif args.exec is not None:
        if not args.exec:
            parser.error("--exec exige um comando")
        environment = dict(os.environ)
        environment["FT_SQL_CONNECTION"] = ("Server=127.0.0.1,51433;Database=FechaTurma;User Id=fechaturma_runtime;"
            f"Password={config['SQL_APP_PASSWORD']};Encrypt=True;TrustServerCertificate=True;Connect Timeout=5")
        environment["FT_ORACLE_CONNECTION"] = ("Data Source=127.0.0.1:51521/XEPDB1;User Id=academic_reader;"
            f"Password={config['ORACLE_READER_PASSWORD']};Connection Timeout=5")
        sys.exit(subprocess.run(args.exec, env=environment, cwd=ROOT).returncode)
    else:
        print("Credenciais locais prontas em .env (ignorado pelo Git).")


if __name__ == "__main__":
    main()
