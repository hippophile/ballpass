.PHONY: setup run test

PROJECT := src/Ballpass.Web

setup:
	dotnet restore
	@docker exec postgres psql -U postgres -tc "SELECT 1 FROM pg_database WHERE datname='ballpass'" | grep -q 1 || docker exec postgres createdb -U postgres ballpass
	@PW=$$(docker exec postgres printenv POSTGRES_PASSWORD); \
	dotnet user-secrets set "ConnectionStrings:Ballpass" "Host=localhost;Port=5432;Database=ballpass;Username=postgres;Password=$$PW" --project $(PROJECT) > /dev/null

run:
	dotnet run --project $(PROJECT)

test:
	dotnet test
