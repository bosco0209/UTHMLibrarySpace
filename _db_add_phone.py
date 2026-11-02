import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
cols = [r[1] for r in con.execute('PRAGMA table_info(Users)')]
if 'Phone' not in cols:
    con.execute("ALTER TABLE Users ADD COLUMN Phone TEXT NOT NULL DEFAULT ''")
    con.commit()
print([r for r in con.execute('PRAGMA table_info(Users)')][-3:])
con.close()
