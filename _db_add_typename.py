import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
cur = con.cursor()
cols = [r[1] for r in cur.execute('PRAGMA table_info(Facilities)')]
if 'TypeName' not in cols:
    cur.execute("ALTER TABLE Facilities ADD COLUMN TypeName TEXT NOT NULL DEFAULT ''")
for row in list(cur.execute('select Id, Description from Facilities')):
    prefix = row[1].split(' — ')[0].strip() if row[1] else ''
    cur.execute('UPDATE Facilities SET TypeName=? WHERE Id=?', (prefix, row[0]))
con.commit()
for row in cur.execute('select Code, Name, Type, TypeName from Facilities order by Code'):
    print(row)
con.close()
