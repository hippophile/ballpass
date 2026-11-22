.PHONY: setup run test reset

PROJECT := src/Ballpass.Web

setup:
	dotnet restore
	@docker exec postgres psql -U postgres -tc "SELECT 1 FROM pg_database WHERE datname='ballpass'" | grep -q 1 || docker exec postgres createdb -U postgres ballpass
	@PW=$$(docker exec postgres printenv POSTGRES_PASSWORD); \
	dotnet user-secrets set "ConnectionStrings:Ballpass" "Host=localhost;Port=5432;Database=ballpass;Username=postgres;Password=$$PW" --project $(PROJECT) > /dev/null
	dotnet run --project $(PROJECT) -- --reset-demo-data

reset:
	dotnet run --project $(PROJECT) -- --reset-demo-data

run:
	dotnet run --project $(PROJECT)

test:
	# A hang (seen twice, right after a new migration) fails after 5 min with a dump instead of blocking forever.
	dotnet test --blame-hang-timeout 5m
