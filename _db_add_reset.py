import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
con.execute('''CREATE TABLE IF NOT EXISTS "PasswordResetRequests" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_PasswordResetRequests" PRIMARY KEY AUTOINCREMENT,
    "Email" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "ExpiresAt" TEXT NOT NULL,
    "Attempts" INTEGER NOT NULL,
    "Verified" INTEGER NOT NULL
)''')
con.commit()
print('ok')
con.close()
