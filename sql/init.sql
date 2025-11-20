-- ============================================
-- XL4Net - Database Initialization Script
-- PostgreSQL 16
-- ============================================
-- Este script roda AUTOMATICAMENTE quando o
-- container PostgreSQL é criado pela primeira vez.
-- ============================================

-- Habilita geração de UUIDs (v4 = random)
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================
-- TABELA: accounts
-- ============================================
-- Armazena contas de usuários
-- UUID = mais seguro que SERIAL (1,2,3...)
-- ============================================
CREATE TABLE accounts (
    -- ID único (UUID v4 = completamente aleatório)
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- Credenciais (ambos unique = não pode duplicar)
    username VARCHAR(50) UNIQUE NOT NULL,
    email VARCHAR(255) UNIQUE NOT NULL,
    
    -- Senha hashada com BCrypt (nunca guarda plain text!)
    -- Exemplo: "$2a$12$N0DF5K5wZ8..." (60 chars)
    password_hash VARCHAR(255) NOT NULL,
    
    -- Metadata extra em JSON (flexível)
    -- Exemplo: {"level": 10, "premium": true}
    metadata JSONB DEFAULT '{}',
    
    -- Timestamps
    created_at TIMESTAMP DEFAULT NOW(),
    last_login TIMESTAMP,
    
    -- Constraints de validação
    CONSTRAINT username_length CHECK (LENGTH(username) >= 3 AND LENGTH(username) <= 50),
    CONSTRAINT email_format CHECK (email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}$')
);

-- Índices para performance
-- B-tree index (default) = ótimo para queries exatas
CREATE INDEX idx_accounts_username ON accounts(username);
CREATE INDEX idx_accounts_email ON accounts(email);

-- GIN index = ótimo para queries JSONB (quando tiver muitos filtros)
CREATE INDEX idx_accounts_metadata ON accounts USING GIN(metadata);

-- Comentários descritivos (aparecem no Adminer)
COMMENT ON TABLE accounts IS 'User accounts with secure authentication';
COMMENT ON COLUMN accounts.id IS 'UUID v4 primary key';
COMMENT ON COLUMN accounts.password_hash IS 'BCrypt hash (cost factor 12)';
COMMENT ON COLUMN accounts.metadata IS 'Flexible JSON storage for user data';

-- ============================================
-- TABELA: login_attempts
-- ============================================
-- Armazena todas as tentativas de login
-- Usado para: rate limiting, security audit
-- ============================================
CREATE TABLE login_attempts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- Referência à conta (NULL = username não existe)
    account_id UUID REFERENCES accounts(id) ON DELETE CASCADE,
    
    -- Endereço IP da tentativa (INET = tipo nativo PostgreSQL)
    ip_address INET NOT NULL,
    
    -- Username tentado (guarda mesmo se não existir)
    username VARCHAR(50),
    
    -- Sucesso ou falha
    success BOOLEAN NOT NULL,
    
    -- Timestamp
    attempted_at TIMESTAMP DEFAULT NOW()
);

-- Índices para queries de rate limiting
-- Exemplo: "quantas tentativas falhas deste IP na última hora?"
CREATE INDEX idx_login_attempts_ip_time ON login_attempts(ip_address, attempted_at);
CREATE INDEX idx_login_attempts_account ON login_attempts(account_id, attempted_at);

-- Índice parcial = só indexa falhas recentes (otimização!)
-- Rate limiting só se importa com falhas dos últimos 60 minutos
CREATE INDEX idx_login_attempts_recent_failures ON login_attempts(ip_address, attempted_at)
    WHERE success = false AND attempted_at > NOW() - INTERVAL '1 hour';

COMMENT ON TABLE login_attempts IS 'Audit log of all login attempts';
COMMENT ON COLUMN login_attempts.ip_address IS 'Client IP address (INET type)';
COMMENT ON COLUMN login_attempts.success IS 'TRUE = successful login, FALSE = failed';

-- ============================================
-- FUNÇÃO: Limpar tentativas antigas
-- ============================================
-- Rate limiting só precisa de dados recentes
-- Roda esta função periodicamente (ou via cron job)
-- ============================================
CREATE OR REPLACE FUNCTION cleanup_old_login_attempts()
RETURNS void AS $$
BEGIN
    -- Deleta tentativas com mais de 7 dias
    DELETE FROM login_attempts
    WHERE attempted_at < NOW() - INTERVAL '7 days';
    
    -- Log para ver quanto deletou
    RAISE NOTICE 'Cleaned up old login attempts';
END;
$$ LANGUAGE plpgsql;

-- ============================================
-- FUNÇÃO: Rate Limiting Check
-- ============================================
-- Verifica se IP excedeu limite de tentativas
-- Retorna: quantidade de tentativas na última hora
-- ============================================
CREATE OR REPLACE FUNCTION check_rate_limit(
    p_ip_address INET,
    p_time_window_minutes INTEGER DEFAULT 60,
    p_max_attempts INTEGER DEFAULT 5
)
RETURNS TABLE (
    attempts_count BIGINT,
    is_limited BOOLEAN,
    retry_after_seconds INTEGER
) AS $$
DECLARE
    v_attempts BIGINT;
    v_oldest_attempt TIMESTAMP;
BEGIN
    -- Conta tentativas falhas no time window
    SELECT 
        COUNT(*),
        MIN(attempted_at)
    INTO 
        v_attempts,
        v_oldest_attempt
    FROM login_attempts
    WHERE 
        ip_address = p_ip_address
        AND success = false
        AND attempted_at > NOW() - (p_time_window_minutes || ' minutes')::INTERVAL;
    
    -- Se passou do limite, calcula tempo de espera
    IF v_attempts >= p_max_attempts THEN
        RETURN QUERY SELECT 
            v_attempts,
            TRUE,
            EXTRACT(EPOCH FROM (v_oldest_attempt + (p_time_window_minutes || ' minutes')::INTERVAL - NOW()))::INTEGER;
    ELSE
        RETURN QUERY SELECT 
            v_attempts,
            FALSE,
            0;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- ============================================
-- DADOS DE TESTE (opcional, comentado)
-- ============================================
-- Descomente para criar usuário de teste
-- Senha: "testpass123" (BCrypt hash abaixo)
-- ============================================

-- INSERT INTO accounts (username, email, password_hash, metadata)
-- VALUES (
--     'testuser',
--     'test@xl4net.dev',
--     '$2a$12$N0DF5K5wZ8XZELAVzSQ9V.qHHkv/BPQl1w4K3F2lW4K3F2lW4K3F2', -- "testpass123"
--     '{"level": 1, "is_test": true}'::JSONB
-- );

-- ============================================
-- VERIFICAÇÃO FINAL
-- ============================================
-- Mostra tabelas criadas
DO $$
BEGIN
    RAISE NOTICE '===========================================';
    RAISE NOTICE 'XL4Net Database initialized successfully!';
    RAISE NOTICE '===========================================';
    RAISE NOTICE 'Tables created:';
    RAISE NOTICE '  - accounts';
    RAISE NOTICE '  - login_attempts';
    RAISE NOTICE 'Functions created:';
    RAISE NOTICE '  - cleanup_old_login_attempts()';
    RAISE NOTICE '  - check_rate_limit()';
    RAISE NOTICE '===========================================';
END $$;